using System.Diagnostics.Metrics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Application.ProcessPayment;
using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Domain.Payments;
using PaymentGateway.Api.Tests.Unit.Fakes;

namespace PaymentGateway.Api.Tests.Unit.Application.ProcessPayment;

public class ProcessPaymentServiceTests
{
    private const string CardNumber = "2222405343248877";
    private const string Cvv = "123";
    private const string AuthorizationCode = "0bb07405-6d44-4b50-a14f-7ae0beff13ad";

    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<BankAuthorizationResult, PaymentStatus> BankDecisions => new()
    {
        { new BankAuthorizationResult.Authorized(AuthorizationCode), PaymentStatus.Authorized },
        { new BankAuthorizationResult.Declined(), PaymentStatus.Declined },
    };

    [Theory]
    [MemberData(nameof(BankDecisions))]
    public async Task Process_WhenBankDecides_ReturnsProcessedAndRecordsPayment(BankAuthorizationResult decision, PaymentStatus expectedStatus)
    {
        // Arrange
        FakeAcquiringBank bank = new(decision);
        FakePaymentRepository repository = new();
        ProcessPaymentService service = CreateService(bank, repository);

        // Act
        ProcessPaymentResult result = await ProcessValidAsync(service);

        // Assert
        Payment payment = Assert.IsType<ProcessPaymentResult.Processed>(result).Payment;
        Assert.Equal(expectedStatus, payment.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal(4, payment.ExpiryMonth);
        Assert.Equal(2027, payment.ExpiryYear);
        Assert.Equal("GBP", payment.Currency);
        Assert.Equal(100, payment.Amount);
        Assert.Equal(1, bank.CallCount);
        Assert.Equal(CardNumber, bank.LastRequest?.CardNumber);
        Assert.Equal(Cvv, bank.LastRequest?.Cvv);
        Assert.Same(payment, Assert.Single(repository.Payments));
    }

    [Fact]
    public async Task Process_WhenBankAuthorizes_RecordsTheAuthorizationCode()
    {
        // Arrange
        FakePaymentRepository repository = new();
        ProcessPaymentService service = CreateService(new FakeAcquiringBank(new BankAuthorizationResult.Authorized(AuthorizationCode)), repository);

        // Act
        await ProcessValidAsync(service);

        // Assert
        Assert.Equal(AuthorizationCode, Assert.Single(repository.Payments).AuthorizationCode);
    }

    [Fact]
    public async Task Process_WhenCommandIsInvalid_ReturnsRejectedWithoutCallingBank()
    {
        // Arrange
        FakeAcquiringBank bank = new(new BankAuthorizationResult.Authorized(AuthorizationCode));
        FakePaymentRepository repository = new();
        ProcessPaymentService service = CreateService(bank, repository);

        // Act
        ProcessPaymentResult result = await service.ProcessAsync(
            cardNumber: "1234", expiryMonth: null, expiryYear: null, currency: "gbp", amount: 0, cvv: null);

        // Assert
        IReadOnlyList<ValidationError> errors = Assert.IsType<ProcessPaymentResult.Rejected>(result).Errors;
        Assert.Equal(
            [nameof(PaymentRequest.CardNumber),
                nameof(PaymentRequest.ExpiryMonth),
                nameof(PaymentRequest.ExpiryYear),
                nameof(PaymentRequest.Currency),
                nameof(PaymentRequest.Amount),
                nameof(PaymentRequest.Cvv)],
            errors.Select(error => error.Field));
        Assert.Equal(0, bank.CallCount);
        Assert.Empty(repository.Payments);
    }

    [Theory]
    [InlineData(BankFailureKind.Unavailable)]
    [InlineData(BankFailureKind.Error)]
    [InlineData(BankFailureKind.OutcomeUnknown)]
    public async Task Process_WhenBankFails_ReturnsBankFailedWithoutRecording(BankFailureKind kind)
    {
        // Arrange
        FakeAcquiringBank bank = new(new BankAuthorizationResult.Failed(kind));
        FakePaymentRepository repository = new();
        ProcessPaymentService service = CreateService(bank, repository);

        // Act
        ProcessPaymentResult result = await ProcessValidAsync(service);

        // Assert
        ProcessPaymentResult.BankFailed failed = Assert.IsType<ProcessPaymentResult.BankFailed>(result);
        Assert.Equal(kind, failed.Kind);
        Assert.NotEqual(Guid.Empty, failed.PaymentId);
        Assert.Equal(1, bank.CallCount);
        Assert.Empty(repository.Payments);
    }

    [Theory]
    [MemberData(nameof(BankDecisions))]
    public async Task Process_WhenBankDecides_LogsPaymentProcessed(BankAuthorizationResult decision, PaymentStatus expectedStatus)
    {
        // Arrange
        FakeAcquiringBank bank = new(decision);
        FakeLogger<ProcessPaymentService> logger = new();
        ProcessPaymentService service = CreateService(bank, new FakePaymentRepository(), logger);

        // Act
        ProcessPaymentResult result = await ProcessValidAsync(service);

        // Assert
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(1000, record.Id.Id);
        Assert.Equal("PaymentProcessed", record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(((ProcessPaymentResult.Processed)result).Payment.Id.ToString(), record.GetStructuredStateValue("paymentId"));
        Assert.Equal(expectedStatus.ToString(), record.GetStructuredStateValue("status"));
        Assert.Equal("GBP", record.GetStructuredStateValue("currency"));
        Assert.Equal("100", record.GetStructuredStateValue("amount"));
        AssertNoCardData(record);
    }

    [Theory]
    [MemberData(nameof(BankDecisions))]
    public async Task Process_WhenRecordingFails_LogsPaymentNotRecordedAndRethrows(BankAuthorizationResult decision, PaymentStatus expectedStatus)
    {
        // Arrange
        FakeAcquiringBank bank = new(decision);
        InvalidOperationException failure = new("store unavailable");
        FakeLogger<ProcessPaymentService> logger = new();
        ProcessPaymentService service = CreateService(bank, new FakePaymentRepository { AddFailure = failure }, logger);

        // Act
        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => ProcessValidAsync(service));

        // Assert
        Assert.Same(failure, thrown);
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(1002, record.Id.Id);
        Assert.Equal("PaymentNotRecorded", record.Id.Name);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Same(failure, record.Exception);
        Assert.Equal(bank.LastPaymentId.ToString(), record.GetStructuredStateValue("paymentId"));
        Assert.Equal(expectedStatus.ToString(), record.GetStructuredStateValue("status"));
        Assert.Equal(expectedStatus == PaymentStatus.Authorized ? AuthorizationCode : null, record.GetStructuredStateValue("authorizationCode"));
        Assert.Equal("GBP", record.GetStructuredStateValue("currency"));
        Assert.Equal("100", record.GetStructuredStateValue("amount"));
        AssertNoCardData(record);
    }

    [Fact]
    public async Task Process_WhenCommandIsInvalid_LogsPaymentRejectedWithFieldNamesOnly()
    {
        // Arrange
        FakeLogger<ProcessPaymentService> logger = new();
        ProcessPaymentService service = CreateService(new FakeAcquiringBank(new BankAuthorizationResult.Authorized(AuthorizationCode)), new FakePaymentRepository(), logger);

        // Act
        await service.ProcessAsync(
            cardNumber: CardNumber + "x", expiryMonth: 4, expiryYear: 2027, currency: "gbp", amount: 100, cvv: Cvv);

        // Assert
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(1001, record.Id.Id);
        Assert.Equal("PaymentRejected", record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("CardNumber,Currency", record.GetStructuredStateValue("invalidFields"));
        AssertNoCardData(record);
    }

    [Theory]
    [InlineData(BankFailureKind.Unavailable)]
    [InlineData(BankFailureKind.Error)]
    [InlineData(BankFailureKind.OutcomeUnknown)]
    public async Task Process_WhenBankFails_PassesThePaymentIdToTheBankPort(BankFailureKind kind)
    {
        // Arrange
        FakeAcquiringBank bank = new(new BankAuthorizationResult.Failed(kind));
        ProcessPaymentService service = CreateService(bank, new FakePaymentRepository());

        // Act
        ProcessPaymentResult result = await ProcessValidAsync(service);

        // Assert
        Assert.Equal(Assert.IsType<ProcessPaymentResult.BankFailed>(result).PaymentId, bank.LastPaymentId);
    }

    public static TheoryData<BankAuthorizationResult, string> BankOutcomes => new()
    {
        { new BankAuthorizationResult.Authorized(AuthorizationCode), "authorized" },
        { new BankAuthorizationResult.Declined(), "declined" },
        { new BankAuthorizationResult.Failed(BankFailureKind.Unavailable), "bank_unavailable" },
        { new BankAuthorizationResult.Failed(BankFailureKind.Error), "bank_error" },
        { new BankAuthorizationResult.Failed(BankFailureKind.OutcomeUnknown), "bank_outcome_unknown" },
    };

    [Theory]
    [MemberData(nameof(BankOutcomes))]
    public async Task Process_WhenBankAnswers_CountsTheOutcomeOnce(BankAuthorizationResult decision, string expectedTag)
    {
        // Arrange
        IMeterFactory meterFactory = CreateMeterFactory();
        using MetricCollector<long> outcomes = OutcomesCollector(meterFactory);
        ProcessPaymentService service = CreateService(new FakeAcquiringBank(decision), new FakePaymentRepository(), meterFactory: meterFactory);

        // Act
        await ProcessValidAsync(service);

        // Assert
        CollectedMeasurement<long> measurement = Assert.Single(outcomes.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal(expectedTag, measurement.Tags[PaymentMetrics.OutcomeTag]);
    }

    [Fact]
    public async Task Process_WhenCommandIsInvalid_CountsARejection()
    {
        // Arrange
        IMeterFactory meterFactory = CreateMeterFactory();
        using MetricCollector<long> outcomes = OutcomesCollector(meterFactory);
        ProcessPaymentService service = CreateService(
            new FakeAcquiringBank(new BankAuthorizationResult.Authorized(AuthorizationCode)), new FakePaymentRepository(), meterFactory: meterFactory);

        // Act
        await service.ProcessAsync(
            cardNumber: null, expiryMonth: null, expiryYear: null, currency: "gbp", amount: null, cvv: null);

        // Assert
        CollectedMeasurement<long> measurement = Assert.Single(outcomes.GetMeasurementSnapshot());
        Assert.Equal("rejected", measurement.Tags[PaymentMetrics.OutcomeTag]);
    }

    private static Task<ProcessPaymentResult> ProcessValidAsync(ProcessPaymentService service)
    {
        return service.ProcessAsync(cardNumber: CardNumber, expiryMonth: 4, expiryYear: 2027, currency: "GBP", amount: 100, cvv: Cvv);
    }

    private static MetricCollector<long> OutcomesCollector(IMeterFactory meterFactory)
    {
        return new MetricCollector<long>(meterFactory, PaymentMetrics.MeterName, PaymentMetrics.OutcomesInstrument);
    }

    private static IMeterFactory CreateMeterFactory()
    {
        return new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
    }

    private static ProcessPaymentService CreateService(
        FakeAcquiringBank bank,
        FakePaymentRepository repository,
        FakeLogger<ProcessPaymentService>? logger = null,
        IMeterFactory? meterFactory = null,
        TimeProvider? timeProvider = null)
    {
        return new ProcessPaymentService(
            bank,
            repository,
            new PaymentMetrics(meterFactory ?? CreateMeterFactory()),
            timeProvider ?? new FakeTimeProvider(Now),
            logger ?? new FakeLogger<ProcessPaymentService>());
    }

    private static void AssertNoCardData(FakeLogRecord record)
    {
        string text = record.Message + string.Join(";", record.StructuredState ?? []);
        Assert.DoesNotContain(CardNumber, text);
        Assert.DoesNotContain("cvv", text, StringComparison.OrdinalIgnoreCase);
    }
}
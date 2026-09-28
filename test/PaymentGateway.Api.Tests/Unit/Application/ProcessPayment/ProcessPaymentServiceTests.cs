using System.Diagnostics.Metrics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

using PaymentGateway.Api.Application.Observability;
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

    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly ProcessPaymentCommand ValidCommand = new()
    {
        CardNumber = CardNumber,
        ExpiryMonth = 4,
        ExpiryYear = 2027,
        Currency = "GBP",
        Amount = 100,
        Cvv = Cvv,
    };

    public static TheoryData<BankAuthorizationResult, PaymentStatus> BankDecisions => new()
    {
        { new BankAuthorizationResult.Authorized(), PaymentStatus.Authorized },
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
        ProcessPaymentResult result = await service.ProcessAsync(ValidCommand);

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
    public async Task Process_WhenCommandIsInvalid_ReturnsRejectedWithoutCallingBank()
    {
        // Arrange
        FakeAcquiringBank bank = new(new BankAuthorizationResult.Authorized());
        FakePaymentRepository repository = new();
        ProcessPaymentService service = CreateService(bank, repository);
        ProcessPaymentCommand command = new() { CardNumber = "1234", Currency = "gbp", Amount = 0 };

        // Act
        ProcessPaymentResult result = await service.ProcessAsync(command);

        // Assert
        IReadOnlyList<ValidationError> errors = Assert.IsType<ProcessPaymentResult.Rejected>(result).Errors;
        Assert.Equal(
            [PaymentField.CardNumber, PaymentField.ExpiryMonth, PaymentField.ExpiryYear, PaymentField.Currency, PaymentField.Amount, PaymentField.Cvv],
            errors.Select(error => error.Field));
        Assert.Equal(0, bank.CallCount);
        Assert.Empty(repository.Payments);
    }

    [Theory]
    [InlineData(BankFailureKind.Unavailable)]
    [InlineData(BankFailureKind.Error)]
    public async Task Process_WhenBankFails_ReturnsBankFailedWithoutRecording(BankFailureKind kind)
    {
        // Arrange
        FakeAcquiringBank bank = new(new BankAuthorizationResult.Failed(kind));
        FakePaymentRepository repository = new();
        ProcessPaymentService service = CreateService(bank, repository);

        // Act
        ProcessPaymentResult result = await service.ProcessAsync(ValidCommand);

        // Assert
        Assert.Equal(kind, Assert.IsType<ProcessPaymentResult.BankFailed>(result).Kind);
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
        ProcessPaymentResult result = await service.ProcessAsync(ValidCommand);

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

    [Fact]
    public async Task Process_WhenCommandIsInvalid_LogsPaymentRejectedWithFieldNamesOnly()
    {
        // Arrange
        FakeLogger<ProcessPaymentService> logger = new();
        ProcessPaymentService service = CreateService(new FakeAcquiringBank(new BankAuthorizationResult.Authorized()), new FakePaymentRepository(), logger);
        ProcessPaymentCommand command = new()
        {
            CardNumber = CardNumber + "x",
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "gbp",
            Amount = 100,
            Cvv = Cvv,
        };

        // Act
        await service.ProcessAsync(command);

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
    public async Task Process_WhenBankFails_LogsPaymentBankFailedAsWarning(BankFailureKind kind)
    {
        // Arrange
        FakeLogger<ProcessPaymentService> logger = new();
        ProcessPaymentService service = CreateService(new FakeAcquiringBank(new BankAuthorizationResult.Failed(kind)), new FakePaymentRepository(), logger);

        // Act
        await service.ProcessAsync(ValidCommand);

        // Assert
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(1002, record.Id.Id);
        Assert.Equal("PaymentBankFailed", record.Id.Name);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(kind.ToString(), record.GetStructuredStateValue("failureKind"));
        Assert.Equal("GBP", record.GetStructuredStateValue("currency"));
        Assert.Equal("100", record.GetStructuredStateValue("amount"));
        AssertNoCardData(record);
    }

    [Theory]
    [InlineData("valid", "authorized")]
    [InlineData("valid", "declined")]
    [InlineData("invalid", "rejected")]
    [InlineData("valid", "bank_unavailable")]
    [InlineData("valid", "bank_error")]
    public async Task Process_ForEachOutcome_RecordsOneOutcomeMeasurement(string commandKind, string expectedResult)
    {
        // Arrange
        BankAuthorizationResult bankResult = expectedResult switch
        {
            "declined" => new BankAuthorizationResult.Declined(),
            "bank_unavailable" => new BankAuthorizationResult.Failed(BankFailureKind.Unavailable),
            "bank_error" => new BankAuthorizationResult.Failed(BankFailureKind.Error),
            _ => new BankAuthorizationResult.Authorized(),
        };
        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        using MetricCollector<long> collector = new(meterFactory, "PaymentGateway", "paymentgateway.payments.outcomes");
        ProcessPaymentService service = CreateService(new FakeAcquiringBank(bankResult), new FakePaymentRepository(), metrics: new PaymentGatewayMetrics(meterFactory));
        ProcessPaymentCommand command = commandKind == "valid" ? ValidCommand : new ProcessPaymentCommand();

        // Act
        await service.ProcessAsync(command);

        // Assert
        CollectedMeasurement<long> measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal(expectedResult, measurement.Tags["result"]);
    }

    private static ProcessPaymentService CreateService(
        FakeAcquiringBank bank,
        FakePaymentRepository repository,
        FakeLogger<ProcessPaymentService>? logger = null,
        PaymentGatewayMetrics? metrics = null)
    {
        return new ProcessPaymentService(
            bank,
            repository,
            new FakeTimeProvider(Now),
            logger ?? new FakeLogger<ProcessPaymentService>(),
            metrics ?? new PaymentGatewayMetrics(new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>()));
    }

    private static void AssertNoCardData(FakeLogRecord record)
    {
        string text = record.Message + string.Join(";", record.StructuredState ?? []);
        Assert.DoesNotContain(CardNumber, text);
        Assert.DoesNotContain("cvv", text, StringComparison.OrdinalIgnoreCase);
    }
}
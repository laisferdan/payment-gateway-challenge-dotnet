using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using PaymentGateway.Api.Application.RetrievePayment;
using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Domain.Payments;
using PaymentGateway.Api.Tests.TestData;
using PaymentGateway.Api.Tests.Unit.Fakes;

namespace PaymentGateway.Api.Tests.Unit.Application.RetrievePayment;

public class RetrievePaymentServiceTests
{
    private static readonly PaymentRequest Request = ValidPaymentRequest.Create();

    [Fact]
    public async Task Retrieve_WhenPaymentExists_ReturnsFoundWithTheRecordedPayment()
    {
        // Arrange
        FakePaymentRepository repository = new();
        Payment payment = Payment.Create(Request, PaymentStatus.Authorized);
        await repository.AddAsync(payment);
        RetrievePaymentService service = CreateService(repository);

        // Act
        RetrievePaymentResult result = await service.RetrieveAsync(payment.Id);

        // Assert
        Assert.Same(payment, Assert.IsType<RetrievePaymentResult.Found>(result).Payment);
    }

    [Fact]
    public async Task Retrieve_WhenPaymentExists_LogsPaymentRetrieved()
    {
        // Arrange
        FakePaymentRepository repository = new();
        Payment payment = Payment.Create(Request, PaymentStatus.Declined);
        await repository.AddAsync(payment);
        FakeLogger<RetrievePaymentService> logger = new();
        RetrievePaymentService service = CreateService(repository, logger);

        // Act
        await service.RetrieveAsync(payment.Id);

        // Assert
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(3000, record.Id.Id);
        Assert.Equal("PaymentRetrieved", record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(payment.Id.ToString(), record.GetStructuredStateValue("paymentId"));
        Assert.Equal("Declined", record.GetStructuredStateValue("status"));
    }

    [Fact]
    public async Task Retrieve_WhenPaymentDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        RetrievePaymentService service = CreateService(new FakePaymentRepository());

        // Act
        RetrievePaymentResult result = await service.RetrieveAsync(Guid.NewGuid());

        // Assert
        Assert.IsType<RetrievePaymentResult.NotFound>(result);
    }

    [Fact]
    public async Task Retrieve_WhenPaymentDoesNotExist_LogsPaymentNotFound()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        FakeLogger<RetrievePaymentService> logger = new();
        RetrievePaymentService service = CreateService(new FakePaymentRepository(), logger);

        // Act
        await service.RetrieveAsync(id);

        // Assert
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(3001, record.Id.Id);
        Assert.Equal("PaymentNotFound", record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(id.ToString(), record.GetStructuredStateValue("paymentId"));
    }

    private static RetrievePaymentService CreateService(FakePaymentRepository repository, FakeLogger<RetrievePaymentService>? logger = null)
    {
        return new RetrievePaymentService(repository, logger ?? new FakeLogger<RetrievePaymentService>());
    }
}
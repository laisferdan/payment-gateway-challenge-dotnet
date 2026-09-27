using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using PaymentGateway.Api.Application;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Tests.Unit.Fakes;

namespace PaymentGateway.Api.Tests.Unit.Application;

public class RetrievePaymentServiceTests
{
    private static readonly PaymentRequest Request = PaymentRequest.Create(
        "2222405343248877", 4, 2027, "GBP", 100, "123", new DateOnly(2026, 9, 26)).Request!;

    [Fact]
    public void Retrieve_WhenPaymentExists_ReturnsFoundWithTheRecordedPayment()
    {
        // Arrange
        FakePaymentRepository repository = new();
        Payment payment = Payment.Create(Request, PaymentStatus.Authorized);
        repository.Add(payment);
        RetrievePaymentService service = CreateService(repository);

        // Act
        RetrievePaymentResult result = service.Retrieve(payment.Id);

        // Assert
        Assert.Same(payment, Assert.IsType<RetrievePaymentResult.Found>(result).Payment);
    }

    [Fact]
    public void Retrieve_WhenPaymentExists_LogsPaymentRetrieved()
    {
        // Arrange
        FakePaymentRepository repository = new();
        Payment payment = Payment.Create(Request, PaymentStatus.Declined);
        repository.Add(payment);
        FakeLogger<RetrievePaymentService> logger = new();
        RetrievePaymentService service = CreateService(repository, logger);

        // Act
        service.Retrieve(payment.Id);

        // Assert
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(3000, record.Id.Id);
        Assert.Equal("PaymentRetrieved", record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(payment.Id.ToString(), record.GetStructuredStateValue("paymentId"));
        Assert.Equal("Declined", record.GetStructuredStateValue("status"));
    }

    [Fact]
    public void Retrieve_WhenPaymentDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        RetrievePaymentService service = CreateService(new FakePaymentRepository());

        // Act
        RetrievePaymentResult result = service.Retrieve(Guid.NewGuid());

        // Assert
        Assert.IsType<RetrievePaymentResult.NotFound>(result);
    }

    [Fact]
    public void Retrieve_WhenPaymentDoesNotExist_LogsPaymentNotFound()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        FakeLogger<RetrievePaymentService> logger = new();
        RetrievePaymentService service = CreateService(new FakePaymentRepository(), logger);

        // Act
        service.Retrieve(id);

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
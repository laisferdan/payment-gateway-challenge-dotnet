using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Infrastructure;

namespace PaymentGateway.Api.Tests.Unit.Infrastructure;

public class InMemoryPaymentRepositoryTests
{
    private static readonly PaymentRequest Request = PaymentRequest.Create(
        "2222405343248877", 4, 2027, "GBP", 100, "123", new DateOnly(2026, 9, 26)).Request!;

    [Fact]
    public void AddThenGetById_ReturnsTheSamePayment()
    {
        // Arrange
        InMemoryPaymentRepository repository = new();
        Payment payment = Payment.Create(Request, PaymentStatus.Authorized);
        repository.Add(payment);

        // Act
        Payment? found = repository.GetById(payment.Id);

        // Assert
        Assert.Same(payment, found);
    }

    [Fact]
    public void GetById_WhenIdIsUnknown_ReturnsNull()
    {
        // Arrange
        InMemoryPaymentRepository repository = new();

        // Act
        Payment? found = repository.GetById(Guid.NewGuid());

        // Assert
        Assert.Null(found);
    }
}
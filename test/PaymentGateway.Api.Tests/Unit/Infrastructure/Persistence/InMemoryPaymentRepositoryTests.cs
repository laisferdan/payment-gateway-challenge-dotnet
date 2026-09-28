using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Domain.Payments;
using PaymentGateway.Api.Infrastructure.Persistence;
using PaymentGateway.Api.Tests.TestData;

namespace PaymentGateway.Api.Tests.Unit.Infrastructure.Persistence;

public class InMemoryPaymentRepositoryTests
{
    private static readonly PaymentRequest Request = ValidPaymentRequest.Create();

    [Fact]
    public async Task AddThenGetById_ReturnsTheSamePayment()
    {
        // Arrange
        InMemoryPaymentRepository repository = new();
        Payment payment = Payment.Create(Request, PaymentStatus.Authorized);
        await repository.AddAsync(payment);

        // Act
        Payment? found = await repository.GetByIdAsync(payment.Id);

        // Assert
        Assert.Same(payment, found);
    }

    [Fact]
    public async Task GetById_WhenIdIsUnknown_ReturnsNull()
    {
        // Arrange
        InMemoryPaymentRepository repository = new();

        // Act
        Payment? found = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(found);
    }
}
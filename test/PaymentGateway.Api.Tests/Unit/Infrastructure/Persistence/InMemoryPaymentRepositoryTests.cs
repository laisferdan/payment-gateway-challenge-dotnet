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
        Payment payment = Payment.Authorized(Guid.NewGuid(), Request, "auth-code");
        await repository.AddAsync(payment);

        // Act
        Payment? found = await repository.GetByIdAsync(payment.Id, CancellationToken.None);

        // Assert
        Assert.Same(payment, found);
    }

    [Fact]
    public async Task AddAsync_WhenIdAlreadyExists_Throws()
    {
        // Arrange
        InMemoryPaymentRepository repository = new();
        Guid id = Guid.NewGuid();
        await repository.AddAsync(Payment.Authorized(id, Request, "auth-code"));

        // Act
        Task Duplicate() => repository.AddAsync(Payment.Declined(id, Request));

        // Assert – AddAsync throws synchronously, so the call is deferred into ThrowsAsync.
        await Assert.ThrowsAsync<InvalidOperationException>(Duplicate);
    }

    [Fact]
    public async Task GetById_WhenIdIsUnknown_ReturnsNull()
    {
        // Arrange
        InMemoryPaymentRepository repository = new();

        // Act
        Payment? found = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public async Task GetById_WhenCancelled_IsCancelled()
    {
        // Arrange
        InMemoryPaymentRepository repository = new();
        Payment payment = Payment.Authorized(Guid.NewGuid(), Request, "auth-code");
        await repository.AddAsync(payment);

        // Act
        Task<Payment?> lookup = repository.GetByIdAsync(payment.Id, new CancellationToken(canceled: true));

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup);
    }
}
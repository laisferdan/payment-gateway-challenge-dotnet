using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Ports;

/// <summary>
/// Driven port that stores payments processed by the gateway. Asynchronous because storage is I/O,
/// whatever the adapter behind it.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Adds a processed payment to the store.
    /// </summary>
    Task AddAsync(Payment payment);

    /// <summary>
    /// Returns the payment recorded under <paramref name="id"/>, or <c>null</c> when none exists.
    /// Has no side effects and is safe to call concurrently with <see cref="AddAsync"/>.
    /// </summary>
    Task<Payment?> GetByIdAsync(Guid id);
}
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

/// <summary>
/// Driven port that stores payments processed by the gateway.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Adds a processed payment to the store.
    /// </summary>
    void Add(Payment payment);

    /// <summary>
    /// Returns the payment recorded under <paramref name="id"/>, or <c>null</c> when none exists.
    /// Has no side effects and is safe to call concurrently with <see cref="Add"/>.
    /// </summary>
    Payment? GetById(Guid id);
}
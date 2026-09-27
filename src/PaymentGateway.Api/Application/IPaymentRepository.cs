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
}
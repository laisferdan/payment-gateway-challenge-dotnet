using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

/// <summary>
/// Driven port: where decided payments are recorded so they can be retrieved later.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>Records an Authorized or Declined payment under its identifier.</summary>
    /// <param name="payment">The payment; it holds no full card number or CVV.</param>
    void Add(Payment payment);
}
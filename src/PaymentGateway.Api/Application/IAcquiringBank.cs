using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

/// <summary>
/// Driven port: the acquiring bank that authorizes or declines a payment.
/// </summary>
public interface IAcquiringBank
{
    /// <summary>
    /// Sends a validated payment request to the bank exactly once – implementations must not retry,
    /// because a payment request is not idempotent.
    /// </summary>
    /// <param name="request">The validated request, including the full card number and CVV.</param>
    /// <param name="cancellationToken">Cancelled when the merchant disconnects.</param>
    /// <returns>The bank's decision, or the kind of failure when no decision was obtained.</returns>
    Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest request, CancellationToken cancellationToken);
}
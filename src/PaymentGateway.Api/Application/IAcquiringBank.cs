using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

/// <summary>
/// Driven port to the acquiring bank that authorizes or declines a payment request.
/// </summary>
public interface IAcquiringBank
{
    /// <summary>
    /// Sends a validated payment request to the bank exactly once – implementations must not retry,
    /// because a payment request is not idempotent.
    /// </summary>
    Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest request);
}
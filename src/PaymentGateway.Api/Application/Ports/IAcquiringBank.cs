using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Application.Ports;

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
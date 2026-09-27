using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

public interface IAcquiringBank
{
    /// <summary>
    /// Sends a validated payment request to the bank exactly once – implementations must not retry,
    /// because a payment request is not idempotent.
    /// </summary>
    Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest request, CancellationToken cancellationToken);
}
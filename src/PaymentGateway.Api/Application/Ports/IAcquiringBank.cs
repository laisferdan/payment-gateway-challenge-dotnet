using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Application.Ports;

public interface IAcquiringBank
{
    /// <summary>
    /// Sends the request exactly once – implementations must not retry: a retry could charge the
    /// shopper twice. 
    /// <paramref name="paymentId"/> is only for logging a failure against the attempt; it is never
    /// sent to the bank.
    /// </summary>
    Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest request, Guid paymentId);
}
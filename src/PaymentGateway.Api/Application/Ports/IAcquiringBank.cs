using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Application.Ports;

/// <summary>
/// Driven port to the acquiring bank that authorizes or declines a payment request.
/// </summary>
public interface IAcquiringBank
{
    /// <summary>
    /// Sends the request exactly once – implementations must not retry, a retry could charge the shopper
    /// twice. No <see cref="CancellationToken"/>: once sent, the payment happens even if the merchant
    /// disconnects, so the call runs to completion (bounded by the bank timeout).
    /// </summary>
    /// <param name="request">The payment to authorize.</param>
    /// <param name="paymentId">
    /// The caller's id for this attempt, carried only so a failure can be logged against it – the
    /// bank itself is never sent or told about it.
    /// </param>
    Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest request, Guid paymentId);
}
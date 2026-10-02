using PaymentGateway.Api.Application.Ports;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

// What the client learned from one bank call: the result it returns, plus what is only logged.
internal sealed record BankCallOutcome(
    BankAuthorizationResult Result,
    int? HttpStatusCode,
    string? FailureReason = null,
    Exception? Exception = null)
{
    public static BankCallOutcome Failed(
        BankFailureKind kind, string failureReason, int? httpStatusCode = null, Exception? exception = null)
    {
        return new BankCallOutcome(new BankAuthorizationResult.Failed(kind), httpStatusCode, failureReason, exception);
    }
}
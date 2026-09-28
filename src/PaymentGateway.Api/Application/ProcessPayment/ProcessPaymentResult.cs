using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.ProcessPayment;

public abstract record ProcessPaymentResult
{
    private ProcessPaymentResult()
    {
    }

    /// <summary>The bank decided (Authorized or Declined) and the payment was recorded.</summary>
    public sealed record Processed(Payment Payment) : ProcessPaymentResult;

    /// <summary>Invalid information was supplied: the bank was not called and nothing was recorded.</summary>
    public sealed record Rejected(IReadOnlyList<ValidationError> Errors) : ProcessPaymentResult;

    /// <summary>The bank gave no decision: nothing was recorded and the request is not retried.</summary>
    public sealed record BankFailed(BankFailureKind Kind) : ProcessPaymentResult;
}
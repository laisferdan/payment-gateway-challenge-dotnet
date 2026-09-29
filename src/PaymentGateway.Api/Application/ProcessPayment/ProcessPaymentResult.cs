using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.ProcessPayment;

public abstract record ProcessPaymentResult
{
    private ProcessPaymentResult()
    {
    }

    public sealed record Processed(Payment Payment) : ProcessPaymentResult;

    public sealed record Rejected(IReadOnlyList<ValidationError> Errors) : ProcessPaymentResult;

    public sealed record BankFailed(Guid PaymentId, BankFailureKind Kind) : ProcessPaymentResult;
}
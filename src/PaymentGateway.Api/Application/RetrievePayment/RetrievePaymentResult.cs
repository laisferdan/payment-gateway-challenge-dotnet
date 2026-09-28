using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.RetrievePayment;

public abstract record RetrievePaymentResult
{
    private RetrievePaymentResult()
    {
    }

    /// <summary>A recorded payment has this id.</summary>
    public sealed record Found(Payment Payment) : RetrievePaymentResult;

    /// <summary>No recorded payment has this id.</summary>
    public sealed record NotFound : RetrievePaymentResult;
}
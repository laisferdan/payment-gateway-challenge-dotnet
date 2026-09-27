using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

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
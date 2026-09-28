namespace PaymentGateway.Api.Domain.PaymentRequests;

public abstract record CreatePaymentRequestResult
{
    private CreatePaymentRequestResult()
    {
    }

    /// <summary>Every rule passed.</summary>
    public sealed record Valid(PaymentRequest Request) : CreatePaymentRequestResult;

    /// <summary>At least one rule is broken; every broken rule is listed.</summary>
    public sealed record Invalid(IReadOnlyList<ValidationError> Errors) : CreatePaymentRequestResult;
}
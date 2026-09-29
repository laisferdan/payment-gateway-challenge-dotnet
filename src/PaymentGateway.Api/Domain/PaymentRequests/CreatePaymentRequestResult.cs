namespace PaymentGateway.Api.Domain.PaymentRequests;

public abstract record CreatePaymentRequestResult
{
    private CreatePaymentRequestResult()
    {
    }

    public sealed record Valid(PaymentRequest Request) : CreatePaymentRequestResult;

    public sealed record Invalid(IReadOnlyList<ValidationError> Errors) : CreatePaymentRequestResult;
}
namespace PaymentGateway.Api.Domain;

public sealed class CreatePaymentRequestResult
{
    private CreatePaymentRequestResult(PaymentRequest? request, IReadOnlyList<ValidationError> errors)
    {
        Request = request;
        Errors = errors;
    }

    public PaymentRequest? Request { get; }

    public IReadOnlyList<ValidationError> Errors { get; }

    public static CreatePaymentRequestResult Valid(PaymentRequest request)
    {
        return new CreatePaymentRequestResult(request, []);
    }

    public static CreatePaymentRequestResult Invalid(IReadOnlyList<ValidationError> errors)
    {
        return new CreatePaymentRequestResult(null, errors);
    }
}
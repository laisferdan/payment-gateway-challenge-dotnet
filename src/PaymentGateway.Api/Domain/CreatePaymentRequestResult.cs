namespace PaymentGateway.Api.Domain;

/// <summary>
/// The outcome of validating a payment request: either the valid request or every rule it broke.
/// </summary>
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
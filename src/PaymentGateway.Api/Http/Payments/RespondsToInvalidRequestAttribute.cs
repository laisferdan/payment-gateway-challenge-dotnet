namespace PaymentGateway.Api.Http.Payments;

/// <summary>
/// Declares how <see cref="InvalidModelStateResponder"/> answers this action when its request cannot
/// be bound. An action without it gets a plain <c>ValidationProblemDetails</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RespondsToInvalidRequestAttribute : Attribute
{
    public RespondsToInvalidRequestAttribute(InvalidRequestResponse response)
    {
        Response = response;
    }

    public InvalidRequestResponse Response { get; }
}
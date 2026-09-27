namespace PaymentGateway.Api.Application;

public enum BankFailureKind
{
    /// <summary>The bank is unavailable (503), did not answer in time or could not be reached; trying later may succeed.</summary>
    Unavailable,

    /// <summary>The bank answered with an error status other than 503, or with a body that cannot be trusted; retrying will not help.</summary>
    Error,
}
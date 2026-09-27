namespace PaymentGateway.Api.Application;

public enum BankFailureKind
{
    /// <summary>The bank is unavailable (503), did not answer in time or could not be reached; trying later may succeed.</summary>
    Unavailable,

    /// <summary>The bank refused the request (400) or answered something unreadable; retrying will not help.</summary>
    Error,
}
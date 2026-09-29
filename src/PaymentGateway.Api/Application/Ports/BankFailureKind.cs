using System.Diagnostics;

namespace PaymentGateway.Api.Application.Ports;

public enum BankFailureKind
{
    /// <summary>503 or never reached: the payment was not made, so retrying is safe.</summary>
    Unavailable,

    /// <summary>Any other error status: the bank refused the request; retrying will not help.</summary>
    Error,

    /// <summary>Timeout, connection lost after sending, or an unreadable 200: it may have been authorized, so it must not be retried.</summary>
    OutcomeUnknown,
}

public static class BankFailureKindExtensions
{
    // The single source for this string: both the response's errorCode and the outcomes metric tag
    // for a bank failure are this value, so the two can never drift apart.
    public static string ToErrorCode(this BankFailureKind kind)
    {
        return kind switch
        {
            BankFailureKind.Unavailable => "bank_unavailable",
            BankFailureKind.Error => "bank_error",
            BankFailureKind.OutcomeUnknown => "bank_outcome_unknown",
            _ => throw new UnreachableException($"Unmapped bank failure {kind}."),
        };
    }
}
using System.Diagnostics;

namespace PaymentGateway.Api.Application.Ports;

public enum BankFailureKind
{
    Unavailable,

    Error,

    OutcomeUnknown,
}

public static class BankFailureKindExtensions
{
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
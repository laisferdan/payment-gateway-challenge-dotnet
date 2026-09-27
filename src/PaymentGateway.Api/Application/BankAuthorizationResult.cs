namespace PaymentGateway.Api.Application;

/// <summary>What the acquiring bank answered for one payment request.</summary>
public abstract record BankAuthorizationResult
{
    private BankAuthorizationResult()
    {
    }

    public sealed record Authorized : BankAuthorizationResult;

    public sealed record Declined : BankAuthorizationResult;

    public sealed record Failed(BankFailureKind Kind) : BankAuthorizationResult;
}
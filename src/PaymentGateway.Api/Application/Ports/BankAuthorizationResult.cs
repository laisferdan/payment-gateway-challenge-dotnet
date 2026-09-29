namespace PaymentGateway.Api.Application.Ports;

public abstract record BankAuthorizationResult
{
    private BankAuthorizationResult()
    {
    }

    public sealed record Authorized(string AuthorizationCode) : BankAuthorizationResult;

    public sealed record Declined : BankAuthorizationResult;

    public sealed record Failed(BankFailureKind Kind) : BankAuthorizationResult;
}
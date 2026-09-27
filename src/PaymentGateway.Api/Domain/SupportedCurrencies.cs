namespace PaymentGateway.Api.Domain;

/// <summary>
/// The ISO 4217 currencies the gateway accepts – no more than three, as the assessment asks.
/// Codes are compared exactly: <c>gbp</c> is not <c>GBP</c>.
/// </summary>
public static class SupportedCurrencies
{
    public const int Length = 3;

    public static readonly IReadOnlyList<string> Codes = ["GBP", "EUR", "USD"];

    public static bool IsSupported(string code)
    {
        return Codes.Contains(code, StringComparer.Ordinal);
    }
}
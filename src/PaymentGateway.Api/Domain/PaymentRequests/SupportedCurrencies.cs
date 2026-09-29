namespace PaymentGateway.Api.Domain.PaymentRequests;

// At most three ISO 4217 codes, as the assessment asks; compared exactly ("gbp" is not "GBP").
public static class SupportedCurrencies
{
    public static readonly IReadOnlyList<string> Codes = ["GBP", "EUR", "USD"];

    public static bool IsSupported(string? code)
    {
        return code is not null && Codes.Contains(code, StringComparer.Ordinal);
    }
}
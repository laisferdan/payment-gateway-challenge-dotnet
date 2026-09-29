namespace PaymentGateway.Api.Domain.PaymentRequests;

public static class SupportedCurrencies
{
    public static readonly IReadOnlyList<string> Codes = ["GBP", "EUR", "USD"];

    public static bool IsSupported(string? code)
    {
        return code is not null && Codes.Contains(code, StringComparer.Ordinal);
    }
}
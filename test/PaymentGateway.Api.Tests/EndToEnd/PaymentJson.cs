using System.Globalization;
using System.Text;

namespace PaymentGateway.Api.Tests.EndToEnd;

internal static class PaymentJson
{
    public const string AuthorizedCard = "2222405343248877";
    public const string DeclinedCard = "2222405343248878";
    public const string UnavailableCard = "2222405343248870";

    public static StringContent Create(params (string Field, string? RawValue)[] overrides)
    {
        Dictionary<string, string?> fields = new()
        {
            ["cardNumber"] = Text(AuthorizedCard),
            ["expiryMonth"] = "12",
            ["expiryYear"] = Number(DateTime.UtcNow.Year + 1),
            ["currency"] = Text("GBP"),
            ["amount"] = "1050",
            ["cvv"] = Text("123"),
        };
        foreach ((string field, string? rawValue) in overrides)
        {
            fields[field] = rawValue;
        }

        string json = "{" + string.Join(",", fields
            .Where(field => field.Value is not null)
            .Select(field => $"\"{field.Key}\":{field.Value}")) + "}";
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    public static (string Field, string? RawValue) Card(string cardNumber)
    {
        return ("cardNumber", Text(cardNumber));
    }

    public static (string Field, string? RawValue)[] Expiry(DateTime month)
    {
        return [("expiryMonth", Number(month.Month)), ("expiryYear", Number(month.Year))];
    }

    private static string Text(string value)
    {
        return $"\"{value}\"";
    }

    private static string Number(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
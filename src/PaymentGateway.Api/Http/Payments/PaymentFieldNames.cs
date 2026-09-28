using System.Text.Json;

using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Http.Payments;

/// <summary>
/// How each <see cref="PaymentField"/> is named on the wire: the JSON name of its
/// <see cref="PostPaymentRequest"/> property, so errors and the OpenAPI document name exactly what
/// the merchant sent.
/// </summary>
public static class PaymentFieldNames
{
    private static readonly IReadOnlyDictionary<PaymentField, string> JsonNames = new Dictionary<PaymentField, string>
    {
        [PaymentField.CardNumber] = JsonName(nameof(PostPaymentRequest.CardNumber)),
        [PaymentField.ExpiryMonth] = JsonName(nameof(PostPaymentRequest.ExpiryMonth)),
        [PaymentField.ExpiryYear] = JsonName(nameof(PostPaymentRequest.ExpiryYear)),
        [PaymentField.Currency] = JsonName(nameof(PostPaymentRequest.Currency)),
        [PaymentField.Amount] = JsonName(nameof(PostPaymentRequest.Amount)),
        [PaymentField.Cvv] = JsonName(nameof(PostPaymentRequest.Cvv)),
    };

    public static string ToJsonName(PaymentField field)
    {
        return JsonNames[field];
    }

    /// <summary>Finds the field named <paramref name="jsonName"/>, ignoring case as JSON binding does.</summary>
    public static bool TryParse(string jsonName, out PaymentField field)
    {
        foreach ((PaymentField candidate, string name) in JsonNames)
        {
            if (string.Equals(name, jsonName, StringComparison.OrdinalIgnoreCase))
            {
                field = candidate;
                return true;
            }
        }

        field = default;
        return false;
    }

    private static string JsonName(string propertyName)
    {
        return JsonNamingPolicy.CamelCase.ConvertName(propertyName);
    }
}
using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

// Nullable so a missing value is detected instead of silently defaulting to false.
internal sealed class BankPaymentResponse
{
    [JsonPropertyName("authorized")]
    public bool? Authorized { get; init; }

    [JsonPropertyName("authorization_code")]
    public string? AuthorizationCode { get; init; }
}
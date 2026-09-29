using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

internal sealed class BankPaymentResponse
{
    [JsonPropertyName("authorized")]
    public bool? Authorized { get; init; }

    [JsonPropertyName("authorization_code")]
    public string? AuthorizationCode { get; init; }
}
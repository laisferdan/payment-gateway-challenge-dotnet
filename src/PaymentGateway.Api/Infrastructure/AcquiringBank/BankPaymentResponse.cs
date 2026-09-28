using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

/// <summary>The acquiring bank's response contract (snake_case). Members are nullable so a missing
/// value can be detected instead of silently defaulting to <c>false</c>.</summary>
internal sealed class BankPaymentResponse
{
    [JsonPropertyName("authorized")]
    public bool? Authorized { get; init; }

    [JsonPropertyName("authorization_code")]
    public string? AuthorizationCode { get; init; }
}
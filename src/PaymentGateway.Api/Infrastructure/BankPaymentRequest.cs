using System.Text.Json.Serialization;

using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Infrastructure;

internal sealed class BankPaymentRequest
{
    [JsonPropertyName("card_number")]
    public required string CardNumber { get; init; }

    /// <summary>Expiry as <c>MM/yyyy</c>.</summary>
    [JsonPropertyName("expiry_date")]
    public required string ExpiryDate { get; init; }

    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    [JsonPropertyName("amount")]
    public required int Amount { get; init; }

    [JsonPropertyName("cvv")]
    public required string Cvv { get; init; }

    public override string ToString()
    {
        return $"BankPaymentRequest {{ card_number = {CardDataMask.MaskCardNumber(CardNumber)}, expiry_date = {ExpiryDate}, currency = {Currency}, amount = {Amount} }}";
    }

    public static BankPaymentRequest From(PaymentRequest request)
    {
        return new BankPaymentRequest
        {
            CardNumber = request.CardNumber,
            ExpiryDate = $"{request.ExpiryMonth:00}/{request.ExpiryYear}",
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv,
        };
    }
}
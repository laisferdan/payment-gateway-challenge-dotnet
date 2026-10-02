using PaymentGateway.Api.Domain.CardData;

namespace PaymentGateway.Api.Application.ProcessPayment;

/// <summary>The use case's input: raw, possibly missing values, validated by <c>PaymentRequest.Create</c>.</summary>
public sealed class ProcessPaymentCommand
{
    public string? CardNumber { get; init; }

    public int? ExpiryMonth { get; init; }

    public int? ExpiryYear { get; init; }

    public string? Currency { get; init; }

    public int? Amount { get; init; }

    public string? Cvv { get; init; }

    public override string ToString()
    {
        return $"ProcessPaymentCommand {{ CardNumber = {CardDataMask.MaskCardNumber(CardNumber)}, Expiry = {ExpiryMonth:00}/{ExpiryYear}, Currency = {Currency}, Amount = {Amount} }}";
    }
}
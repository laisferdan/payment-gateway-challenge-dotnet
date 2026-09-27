using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

/// <summary>
/// A payment request exactly as the merchant sent it: any value may be missing or invalid.
/// </summary>
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
        return $"ProcessPaymentCommand {{ CardNumber = {CardDataMask.MaskCardNumber(CardNumber)}, ExpiryMonth = {ExpiryMonth}, ExpiryYear = {ExpiryYear}, Currency = {Currency}, Amount = {Amount} }}";
    }
}
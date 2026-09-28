using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Http.Payments;

/// <summary>
/// A payment as merchants see it. Never contains the full card number or the CVV.
/// </summary>
public sealed class PaymentResponse
{
    /// <summary>The payment identifier; use it to retrieve the payment later.</summary>
    public required Guid Id { get; init; }

    /// <summary>The acquiring bank's decision: <c>Authorized</c> or <c>Declined</c>.</summary>
    public required PaymentStatus Status { get; init; }

    /// <summary>The last four digits of the card number, as text so leading zeros are kept.</summary>
    public required string CardNumberLastFour { get; init; }

    /// <summary>Expiry month, 1–12.</summary>
    public required int ExpiryMonth { get; init; }

    /// <summary>Expiry year, e.g. 2027.</summary>
    public required int ExpiryYear { get; init; }

    /// <summary>ISO 4217 currency code: GBP, EUR or USD.</summary>
    public required string Currency { get; init; }

    /// <summary>Amount in the minor currency unit (USD $10.50 = 1050).</summary>
    public required int Amount { get; init; }

    public static PaymentResponse From(Payment payment)
    {
        return new PaymentResponse
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount,
        };
    }
}
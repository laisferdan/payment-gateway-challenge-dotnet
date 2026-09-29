using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Http.Payments;

/// <summary>A payment as merchants see it: only the last four card digits, never the CVV.</summary>
/// <param name="Id">The payment identifier; use it to retrieve the payment later.</param>
/// <param name="Status">The acquiring bank's decision: <c>Authorized</c> or <c>Declined</c>.</param>
/// <param name="CardNumberLastFour">The last four digits of the card number, as text so leading zeros are kept.</param>
/// <param name="ExpiryMonth">Expiry month, 1–12.</param>
/// <param name="ExpiryYear">Expiry year, e.g. 2027.</param>
/// <param name="Currency">ISO 4217 currency code: GBP, EUR or USD.</param>
/// <param name="Amount">Amount in the minor currency unit (USD $10.50 = 1050).</param>
public sealed record PaymentResponseDto(
    Guid Id,
    PaymentStatus Status,
    string CardNumberLastFour,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount)
{
    public static PaymentResponseDto From(Payment payment)
    {
        return new PaymentResponseDto(
            payment.Id, payment.Status, payment.CardNumberLastFour, payment.ExpiryMonth, payment.ExpiryYear, payment.Currency, payment.Amount);
    }
}
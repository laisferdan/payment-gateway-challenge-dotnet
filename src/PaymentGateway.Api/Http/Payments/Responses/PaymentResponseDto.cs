using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Http.Payments.Responses;

/// <summary>A payment as merchants see it: only the last four card digits, never the CVV.</summary>
/// <param name="Id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">The payment identifier; use it to retrieve the payment later.</param>
/// <param name="Status" example="Authorized">The acquiring bank's decision: <c>Authorized</c> or <c>Declined</c>.</param>
/// <param name="CardNumberLastFour" example="8877">The last four digits of the card number, as text so leading zeros are kept.</param>
/// <param name="ExpiryMonth" example="4">Expiry month, 1–12.</param>
/// <param name="ExpiryYear" example="2027">Expiry year, e.g. 2027.</param>
/// <param name="Currency" example="GBP">ISO 4217 currency code: GBP, EUR or USD.</param>
/// <param name="Amount" example="1050">Amount in the minor currency unit (USD $10.50 = 1050).</param>
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
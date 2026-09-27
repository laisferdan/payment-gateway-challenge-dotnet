namespace PaymentGateway.Api.Domain;

/// <summary>
/// A payment the acquiring bank has decided on. Holds only data that is safe to store and return:
/// never the full card number, the CVV or the bank's authorization code. Immutable.
/// </summary>
public sealed class Payment
{
    private Payment(Guid id, PaymentStatus status, string cardNumberLastFour, int expiryMonth, int expiryYear, string currency, int amount)
    {
        Id = id;
        Status = status;
        CardNumberLastFour = cardNumberLastFour;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Currency = currency;
        Amount = amount;
    }

    public Guid Id { get; }

    public PaymentStatus Status { get; }

    public string CardNumberLastFour { get; }

    public int ExpiryMonth { get; }

    public int ExpiryYear { get; }

    public string Currency { get; }

    /// <summary>Amount in the minor currency unit.</summary>
    public int Amount { get; }

    /// <summary>Records the bank's decision under a new random (v4, not guessable) identifier.</summary>
    public static Payment Create(PaymentRequest request, PaymentStatus status)
    {
        return new Payment(
            Guid.NewGuid(),
            status,
            request.CardNumberLastFour,
            request.ExpiryMonth,
            request.ExpiryYear,
            request.Currency,
            request.Amount);
    }
}
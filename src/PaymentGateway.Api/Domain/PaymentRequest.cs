namespace PaymentGateway.Api.Domain;

/// <summary>
/// A payment request that passed every validation rule of the assessment. It exists only while
/// the request is handled and is never stored: it carries the full card number and CVV.
/// </summary>
public sealed class PaymentRequest
{
    public const int MinCardNumberLength = 14;
    public const int MaxCardNumberLength = 19;
    public const int LastFourLength = 4;
    public const int MinExpiryMonth = 1;
    public const int MaxExpiryMonth = 12;

    // The bank contract sends the expiry as MM/yyyy and DateOnly stops at 9999, so a later year
    // must be Rejected here rather than fail later as an unexpected error.
    public const int MaxExpiryYear = 9999;
    public const int MinAmount = 1;
    public const int MinCvvLength = 3;
    public const int MaxCvvLength = 4;

    private PaymentRequest(string cardNumber, int expiryMonth, int expiryYear, string currency, int amount, string cvv)
    {
        CardNumber = cardNumber;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Currency = currency;
        Amount = amount;
        Cvv = cvv;
    }

    public string CardNumber { get; }

    public int ExpiryMonth { get; }

    public int ExpiryYear { get; }

    public string Currency { get; }

    /// <summary>Amount in the minor currency unit (USD $10.50 = 1050).</summary>
    public int Amount { get; }

    public string Cvv { get; }

    public string CardNumberLastFour => CardNumber[^LastFourLength..];

    public override string ToString()
    {
        return $"PaymentRequest {{ CardNumber = {CardDataMask.MaskCardNumber(CardNumber)}, Expiry = {ExpiryMonth:00}/{ExpiryYear}, Currency = {Currency}, Amount = {Amount} }}";
    }

    /// <summary>
    /// Checks every field and returns either the valid request or all broken rules together.
    /// Values are never trimmed, padded, case-converted or defaulted into validity.
    /// </summary>
    public static CreatePaymentRequestResult Create(
        string? cardNumber,
        int? expiryMonth,
        int? expiryYear,
        string? currency,
        int? amount,
        string? cvv,
        DateOnly today)
    {
        List<ValidationError> errors = [];

        if (!IsDigitsOfLength(cardNumber, MinCardNumberLength, MaxCardNumberLength))
        {
            errors.Add(new ValidationError(
                Fields.CardNumber,
                $"Card number must be {MinCardNumberLength} to {MaxCardNumberLength} characters long and contain only digits 0-9."));
        }

        if (expiryMonth is not (>= MinExpiryMonth and <= MaxExpiryMonth))
        {
            errors.Add(new ValidationError(
                Fields.ExpiryMonth,
                $"Expiry month must be between {MinExpiryMonth} and {MaxExpiryMonth}."));
        }

        bool yearInRange = expiryYear is int year && year >= today.Year && year <= MaxExpiryYear;
        if (!yearInRange)
        {
            errors.Add(new ValidationError(
                Fields.ExpiryYear,
                $"Expiry year must be a full year, not in the past and at most {MaxExpiryYear}."));
        }
        else if (errors.All(error => error.Field != Fields.ExpiryMonth) && IsBeforeCurrentMonth(expiryMonth!.Value, expiryYear!.Value, today))
        {
            // Checked only when month and year are each valid, so one bad field does not produce a
            // misleading second error.
            errors.Add(new ValidationError(
                Fields.ExpiryYear,
                "The card has expired: expiry month and year must not be before the current month."));
        }

        if (currency is null || currency.Length != SupportedCurrencies.Length || !SupportedCurrencies.IsSupported(currency))
        {
            errors.Add(new ValidationError(
                Fields.Currency,
                $"Currency must be one of: {string.Join(", ", SupportedCurrencies.Codes)}."));
        }

        if (amount is not >= MinAmount)
        {
            errors.Add(new ValidationError(
                Fields.Amount,
                $"Amount must be an integer in the minor currency unit of at least {MinAmount}."));
        }

        if (!IsDigitsOfLength(cvv, MinCvvLength, MaxCvvLength))
        {
            errors.Add(new ValidationError(
                Fields.Cvv,
                $"CVV must be {MinCvvLength} to {MaxCvvLength} characters long and contain only digits 0-9."));
        }

        if (errors.Count > 0)
        {
            return CreatePaymentRequestResult.Invalid(errors);
        }

        return CreatePaymentRequestResult.Valid(
            new PaymentRequest(cardNumber!, expiryMonth!.Value, expiryYear!.Value, currency!, amount!.Value, cvv!));
    }

    // A card is valid until the end of its expiry month.
    private static bool IsBeforeCurrentMonth(int expiryMonth, int expiryYear, DateOnly today)
    {
        return (expiryYear, expiryMonth).CompareTo((today.Year, today.Month)) < 0;
    }

    // Only ASCII digits: char.IsDigit would also accept digits of other scripts.
    private static bool IsDigitsOfLength(string? value, int minLength, int maxLength)
    {
        return value is not null
            && value.Length >= minLength
            && value.Length <= maxLength
            && value.All(char.IsAsciiDigit);
    }

    /// <summary>Request field names as the merchant sends them.</summary>
    public static class Fields
    {
        public const string CardNumber = "cardNumber";
        public const string ExpiryMonth = "expiryMonth";
        public const string ExpiryYear = "expiryYear";
        public const string Currency = "currency";
        public const string Amount = "amount";
        public const string Cvv = "cvv";
    }
}
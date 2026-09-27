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
            errors.Add(new ValidationError(Fields.CardNumber, Messages.CardNumber));
        }

        if (expiryMonth is not (>= MinExpiryMonth and <= MaxExpiryMonth))
        {
            errors.Add(new ValidationError(Fields.ExpiryMonth, Messages.ExpiryMonth));
        }

        bool yearInRange = expiryYear is int year && year >= today.Year && year <= MaxExpiryYear;
        if (!yearInRange)
        {
            errors.Add(new ValidationError(Fields.ExpiryYear, Messages.ExpiryYear));
        }
        else if (errors.All(error => error.Field != Fields.ExpiryMonth) && IsBeforeCurrentMonth(expiryMonth!.Value, expiryYear!.Value, today))
        {
            // Checked only when month and year are each valid, so one bad field does not produce a
            // misleading second error.
            errors.Add(new ValidationError(Fields.ExpiryYear, Messages.Expired));
        }

        if (currency is null || currency.Length != SupportedCurrencies.Length || !SupportedCurrencies.IsSupported(currency))
        {
            errors.Add(new ValidationError(Fields.Currency, Messages.Currency));
        }

        if (amount is not >= MinAmount)
        {
            errors.Add(new ValidationError(Fields.Amount, Messages.Amount));
        }

        if (!IsDigitsOfLength(cvv, MinCvvLength, MaxCvvLength))
        {
            errors.Add(new ValidationError(Fields.Cvv, Messages.Cvv));
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

    public static class Fields
    {
        public const string CardNumber = "cardNumber";
        public const string ExpiryMonth = "expiryMonth";
        public const string ExpiryYear = "expiryYear";
        public const string Currency = "currency";
        public const string Amount = "amount";
        public const string Cvv = "cvv";

        public static readonly IReadOnlyList<string> All = [CardNumber, ExpiryMonth, ExpiryYear, Currency, Amount, Cvv];
    }

    /// <summary>
    /// One message per field, stating what a valid value looks like – including whether it is text or
    /// a whole number, so the same message also answers a value of the wrong kind. Never contains the
    /// submitted value.
    /// </summary>
    public static class Messages
    {
        public static readonly string CardNumber =
            $"Card number must be a string of {MinCardNumberLength} to {MaxCardNumberLength} digits (0-9).";

        public static readonly string ExpiryMonth =
            $"Expiry month must be a whole number from {MinExpiryMonth} to {MaxExpiryMonth}.";

        public static readonly string ExpiryYear =
            $"Expiry year must be a whole number with all four digits (e.g. 2027), not in the past and at most {MaxExpiryYear}.";

        public const string Expired = "The expiry month and year must not be before the current month (UTC): the card must not have expired.";

        public static readonly string Currency =
            $"Currency must be one of: {string.Join(", ", SupportedCurrencies.Codes)}.";

        public static readonly string Amount =
            $"Amount must be a whole number of at least {MinAmount}, in the currency's minor unit (e.g. 1050 for 10.50).";

        public static readonly string Cvv =
            $"CVV must be a string of {MinCvvLength} or {MaxCvvLength} digits (0-9).";

        /// <summary>The message for a field's value, used when that value is missing, invalid or of the wrong type.</summary>
        public static string For(string field)
        {
            return field switch
            {
                Fields.CardNumber => CardNumber,
                Fields.ExpiryMonth => ExpiryMonth,
                Fields.ExpiryYear => ExpiryYear,
                Fields.Currency => Currency,
                Fields.Amount => Amount,
                Fields.Cvv => Cvv,
                _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Not a payment request field."),
            };
        }
    }
}
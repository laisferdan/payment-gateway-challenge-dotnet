using PaymentGateway.Api.Domain.CardData;

namespace PaymentGateway.Api.Domain.PaymentRequests;

// Carries the full card number and CVV, so it only lives while the request is handled; never stored.
public sealed class PaymentRequest
{
    public const int MinCardNumberLength = 14;
    public const int MaxCardNumberLength = 19;
    public const int MinExpiryMonth = 1;
    public const int MaxExpiryMonth = 12;

    public const int MaxExpiryYearsAhead = 20;
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

    public int Amount { get; }

    public string Cvv { get; }

    public string CardNumberLastFour => CardNumber[^CardDataMask.LastFourLength..];

    public override string ToString()
    {
        return $"PaymentRequest {{ CardNumber = {CardDataMask.MaskCardNumber(CardNumber)}, Expiry = {ExpiryMonth:00}/{ExpiryYear}, Currency = {Currency}, Amount = {Amount} }}";
    }

    // Reports every broken rule at once; values are never trimmed, padded or case-converted into validity.
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
            errors.Add(new ValidationError(nameof(CardNumber), Messages.CardNumber));
        }

        bool monthValid = expiryMonth is >= MinExpiryMonth and <= MaxExpiryMonth;
        if (!monthValid)
        {
            errors.Add(new ValidationError(nameof(ExpiryMonth), Messages.ExpiryMonth));
        }

        if (expiryYear is not int year || year < today.Year || year > today.Year + MaxExpiryYearsAhead)
        {
            errors.Add(new ValidationError(nameof(ExpiryYear), Messages.ExpiryYear));
        }
        else if (monthValid && expiryMonth is int month && IsBeforeCurrentMonth(month, year, today))
        {
            // Only when both are structurally valid, so one bad field does not add a misleading second
            // error. The combination, not either field alone, is at fault, so both are keyed.
            errors.Add(new ValidationError(nameof(ExpiryMonth), Messages.Expired));
            errors.Add(new ValidationError(nameof(ExpiryYear), Messages.Expired));
        }

        if (!SupportedCurrencies.IsSupported(currency))
        {
            errors.Add(new ValidationError(nameof(Currency), Messages.Currency));
        }

        if (amount is not >= MinAmount)
        {
            errors.Add(new ValidationError(nameof(Amount), Messages.Amount));
        }

        if (!IsDigitsOfLength(cvv, MinCvvLength, MaxCvvLength))
        {
            errors.Add(new ValidationError(nameof(Cvv), Messages.Cvv));
        }

        if (errors.Count > 0)
        {
            return new CreatePaymentRequestResult.Invalid(errors);
        }

        return new CreatePaymentRequestResult.Valid(
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

    // Each message also answers a value of the wrong JSON type, and never echoes the submitted value.
    public static class Messages
    {
        public static readonly string CardNumber =
            $"Card number must be a string of {MinCardNumberLength} to {MaxCardNumberLength} digits (0-9).";

        public static readonly string ExpiryMonth =
            $"Expiry month must be a whole number from {MinExpiryMonth} to {MaxExpiryMonth}.";

        public static readonly string ExpiryYear =
            $"Expiry year must be a whole number with all four digits (e.g. 2027), not in the past and at most {MaxExpiryYearsAhead} years ahead.";

        public const string Expired = "The expiry month and year must not be before the current month (UTC): the card must not have expired.";

        public static readonly string Currency =
            $"Currency must be one of: {string.Join(", ", SupportedCurrencies.Codes)}.";

        public static readonly string Amount =
            $"Amount must be a whole number of at least {MinAmount}, in the currency's minor unit (e.g. 1050 for 10.50).";

        public static readonly string Cvv =
            $"CVV must be a string of {MinCvvLength} or {MaxCvvLength} digits (0-9).";

        private static readonly IReadOnlyDictionary<string, string> ByField = new Dictionary<string, string>
        {
            [nameof(PaymentRequest.CardNumber)] = CardNumber,
            [nameof(PaymentRequest.ExpiryMonth)] = ExpiryMonth,
            [nameof(PaymentRequest.ExpiryYear)] = ExpiryYear,
            [nameof(PaymentRequest.Currency)] = Currency,
            [nameof(PaymentRequest.Amount)] = Amount,
            [nameof(PaymentRequest.Cvv)] = Cvv,
        };

        public static bool TryFor(string field, out string message)
        {
            return ByField.TryGetValue(field, out message!);
        }
    }
}
using PaymentGateway.Api.Application;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Http;

/// <summary>
/// A card payment to process. Every member is nullable so a missing value reaches the validation
/// rules and is reported as Rejected instead of silently defaulting. The rules live in the domain;
/// they are repeated here only as documentation.
/// </summary>
public sealed class PostPaymentRequest
{
    /// <summary>Required. 14–19 characters, digits 0-9 only.</summary>
    /// <example>2222405343248877</example>
    public string? CardNumber { get; init; }

    /// <summary>Required. 1–12.</summary>
    /// <example>4</example>
    public int? ExpiryMonth { get; init; }

    /// <summary>
    /// Required. Full year, not in the past, at most 9999; month + year must not be before the
    /// current month (UTC).
    /// </summary>
    /// <example>2027</example>
    public int? ExpiryYear { get; init; }

    /// <summary>Required. Uppercase ISO 4217 code: GBP, EUR or USD.</summary>
    /// <example>GBP</example>
    public string? Currency { get; init; }

    /// <summary>Required. Integer in the minor currency unit, at least 1 (USD $10.50 = 1050).</summary>
    /// <example>1050</example>
    public int? Amount { get; init; }

    /// <summary>Required. 3–4 characters, digits 0-9 only.</summary>
    /// <example>123</example>
    public string? Cvv { get; init; }

    public override string ToString()
    {
        return $"PostPaymentRequest {{ CardNumber = {CardDataMask.MaskCardNumber(CardNumber)}, ExpiryMonth = {ExpiryMonth}, ExpiryYear = {ExpiryYear}, Currency = {Currency}, Amount = {Amount} }}";
    }

    public ProcessPaymentCommand ToCommand()
    {
        return new ProcessPaymentCommand
        {
            CardNumber = CardNumber,
            ExpiryMonth = ExpiryMonth,
            ExpiryYear = ExpiryYear,
            Currency = Currency,
            Amount = Amount,
            Cvv = Cvv,
        };
    }
}
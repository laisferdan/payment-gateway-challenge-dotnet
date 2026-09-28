using PaymentGateway.Api.Application.ProcessPayment;
using PaymentGateway.Api.Domain.CardData;

namespace PaymentGateway.Api.Http.Payments;

/// <summary>
/// A card payment to process. Every member is nullable so a missing value reaches the validation
/// rules and is reported as Rejected instead of silently defaulting. The rules live in the domain;
/// <see cref="PaymentRuleSchemaFilter"/> documents each field with the domain's rule message.
/// </summary>
public sealed class PostPaymentRequest
{
    /// <example>2222405343248877</example>
    public string? CardNumber { get; init; }

    /// <example>4</example>
    public int? ExpiryMonth { get; init; }

    /// <example>2027</example>
    public int? ExpiryYear { get; init; }

    /// <example>GBP</example>
    public string? Currency { get; init; }

    /// <example>1050</example>
    public int? Amount { get; init; }

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
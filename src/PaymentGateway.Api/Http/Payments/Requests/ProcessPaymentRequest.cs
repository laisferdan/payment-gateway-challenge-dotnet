using PaymentGateway.Api.Application.ProcessPayment;

namespace PaymentGateway.Api.Http.Payments.Requests;

/// <summary>A card payment as the merchant sends it over the wire.</summary>
public sealed class ProcessPaymentRequest
{
    /// <summary>Required. 14 to 19 digits (0-9).</summary>
    /// <example>2222405343248877</example>
    public string? CardNumber { get; init; }

    /// <summary>Required. 1 to 12.</summary>
    /// <example>4</example>
    public int? ExpiryMonth { get; init; }

    /// <summary>Required. Four digits, at most 20 years ahead; with the month, not before the current month (UTC).</summary>
    /// <example>2027</example>
    public int? ExpiryYear { get; init; }

    /// <summary>Required. One of GBP, EUR, USD.</summary>
    /// <example>GBP</example>
    public string? Currency { get; init; }

    /// <summary>Required. Whole number of at least 1, in the minor currency unit (10.50 = 1050).</summary>
    /// <example>1050</example>
    public int? Amount { get; init; }

    /// <summary>Required. 3 or 4 digits (0-9).</summary>
    /// <example>123</example>
    public string? Cvv { get; init; }

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
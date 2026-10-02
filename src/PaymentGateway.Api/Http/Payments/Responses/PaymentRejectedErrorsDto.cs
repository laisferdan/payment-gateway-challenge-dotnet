namespace PaymentGateway.Api.Http.Payments.Responses;

// Documents the "errors" member PaymentResultMapper.PaymentRejected writes; never serialized itself.

/// <summary>Only the invalid fields are present, each with the rule it broke. Never echoes the card number or CVV.</summary>
public sealed class PaymentRejectedErrorsDto
{
    /// <example>["Card number must be a string of 14 to 19 digits (0-9)."]</example>
    public string[]? CardNumber { get; init; }

    /// <example>["Expiry month must be a whole number from 1 to 12."]</example>
    public string[]? ExpiryMonth { get; init; }

    /// <example>["Expiry year must be a whole number with all four digits (e.g. 2027), not in the past and at most 20 years ahead."]</example>
    public string[]? ExpiryYear { get; init; }

    /// <example>["Currency must be one of: GBP, EUR, USD."]</example>
    public string[]? Currency { get; init; }

    /// <example>["Amount must be a whole number of at least 1, in the currency's minor unit (e.g. 1050 for 10.50)."]</example>
    public string[]? Amount { get; init; }

    /// <example>["CVV must be a string of 3 or 4 digits (0-9)."]</example>
    public string[]? Cvv { get; init; }

    /// <summary>Instead of the fields: the body was not a JSON object with the payment fields.</summary>
    /// <example>["The request body must be a JSON object with the payment fields."]</example>
    public string[]? Body { get; init; }
}
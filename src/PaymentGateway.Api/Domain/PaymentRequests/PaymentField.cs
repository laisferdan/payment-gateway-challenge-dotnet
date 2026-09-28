namespace PaymentGateway.Api.Domain.PaymentRequests;

/// <summary>
/// A field of a payment request that a validation rule applies to. How a field is named on the wire
/// is the driving adapter's concern, not the domain's.
/// </summary>
public enum PaymentField
{
    CardNumber,
    ExpiryMonth,
    ExpiryYear,
    Currency,
    Amount,
    Cvv,
}
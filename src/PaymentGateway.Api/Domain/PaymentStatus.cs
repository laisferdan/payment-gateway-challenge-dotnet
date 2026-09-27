namespace PaymentGateway.Api.Domain;

/// <summary>
/// The acquiring bank's decision on a payment. <c>Rejected</c> is not a member: a rejected request
/// never becomes a payment.
/// </summary>
public enum PaymentStatus
{
    Authorized,
    Declined,
}
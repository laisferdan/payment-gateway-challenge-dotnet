namespace PaymentGateway.Api.Domain;

/// <summary>
/// The status of a payment. <c>Authorized</c> and <c>Declined</c> are the acquiring bank's decision;
/// <c>Rejected</c> means the gateway refused invalid information, so the bank was never called and
/// no payment was recorded.
/// </summary>
public enum PaymentStatus
{
    Authorized,
    Declined,
    Rejected,
}
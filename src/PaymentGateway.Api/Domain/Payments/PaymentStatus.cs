namespace PaymentGateway.Api.Domain.Payments;

// No Rejected: a rejected request never reached the bank, so it is not a payment.
public enum PaymentStatus
{
    Authorized,
    Declined,
}
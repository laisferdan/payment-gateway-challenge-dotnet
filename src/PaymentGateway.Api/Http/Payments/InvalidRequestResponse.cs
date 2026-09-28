namespace PaymentGateway.Api.Http.Payments;

/// <summary>How an action answers a request the framework could not bind.</summary>
public enum InvalidRequestResponse
{
    /// <summary>The payment is Rejected like any other invalid payment, with <c>paymentStatus</c>.</summary>
    PaymentRejected,

    /// <summary>The route's payment id is refused as invalid input, without echoing it.</summary>
    InvalidPaymentId,
}
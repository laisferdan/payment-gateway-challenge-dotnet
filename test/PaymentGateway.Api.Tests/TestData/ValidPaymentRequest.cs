using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Tests.TestData;

/// <summary>A payment request that passes every rule, for tests that need one but do not test validation.</summary>
internal static class ValidPaymentRequest
{
    public static readonly DateOnly Today = new(2026, 9, 26);

    public static PaymentRequest Create(string cardNumber = "2222405343248877", string currency = "GBP", int amount = 100, string cvv = "123")
    {
        CreatePaymentRequestResult result = PaymentRequest.Create(cardNumber, 4, 2027, currency, amount, cvv, Today);
        return Assert.IsType<CreatePaymentRequestResult.Valid>(result).Request;
    }
}
using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Domain.Payments;

public sealed class Payment
{
    private Payment(
        Guid id,
        PaymentStatus status,
        string? authorizationCode,
        string cardNumberLastFour,
        int expiryMonth,
        int expiryYear,
        string currency,
        int amount)
    {
        Id = id;
        Status = status;
        AuthorizationCode = authorizationCode;
        CardNumberLastFour = cardNumberLastFour;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Currency = currency;
        Amount = amount;
    }

    public Guid Id { get; }

    public PaymentStatus Status { get; }

    public string? AuthorizationCode { get; }

    public string CardNumberLastFour { get; }

    public int ExpiryMonth { get; }

    public int ExpiryYear { get; }

    public string Currency { get; }

    public int Amount { get; }

    public static Payment Authorized(Guid id, PaymentRequest request, string authorizationCode)
    {
        return Create(id, request, PaymentStatus.Authorized, authorizationCode);
    }

    public static Payment Declined(Guid id, PaymentRequest request)
    {
        return Create(id, request, PaymentStatus.Declined, authorizationCode: null);
    }

    private static Payment Create(Guid id, PaymentRequest request, PaymentStatus status, string? authorizationCode)
    {
        return new Payment(
            id,
            status,
            authorizationCode,
            request.CardNumberLastFour,
            request.ExpiryMonth,
            request.ExpiryYear,
            request.Currency,
            request.Amount);
    }
}
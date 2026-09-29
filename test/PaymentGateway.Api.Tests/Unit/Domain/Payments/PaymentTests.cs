using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Domain.Payments;
using PaymentGateway.Api.Tests.TestData;

namespace PaymentGateway.Api.Tests.Unit.Domain.Payments;

public class PaymentTests
{
    private const string CardNumber = "2222405343240012";
    private const string Cvv = "987";

    private static readonly PaymentRequest Request = ValidPaymentRequest.Create(CardNumber, "EUR", 2500, Cvv);

    [Fact]
    public void Authorized_FromValidRequest_UsesTheGivenIdAndCopiesSafeFieldsWithTheAuthorizationCode()
    {
        // Arrange
        Guid id = Guid.NewGuid();

        // Act
        Payment payment = Payment.Authorized(id, Request, "0bb07405-6d44-4b50-a14f-7ae0beff13ad");

        // Assert
        Assert.Equal(id, payment.Id);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal("0bb07405-6d44-4b50-a14f-7ae0beff13ad", payment.AuthorizationCode);
        Assert.Equal("0012", payment.CardNumberLastFour);
        Assert.Equal(4, payment.ExpiryMonth);
        Assert.Equal(2027, payment.ExpiryYear);
        Assert.Equal("EUR", payment.Currency);
        Assert.Equal(2500, payment.Amount);
    }

    [Fact]
    public void Declined_FromValidRequest_UsesTheGivenIdAndCopiesSafeFieldsWithoutAnAuthorizationCode()
    {
        // Arrange
        Guid id = Guid.NewGuid();

        // Act
        Payment payment = Payment.Declined(id, Request);

        // Assert
        Assert.Equal(id, payment.Id);
        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Null(payment.AuthorizationCode);
        Assert.Equal("0012", payment.CardNumberLastFour);
        Assert.Equal(2500, payment.Amount);
    }
}
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Tests.Unit.Domain;

public class PaymentTests
{
    private const string CardNumber = "2222405343240012";
    private const string Cvv = "987";

    private static readonly PaymentRequest Request = PaymentRequest.Create(
        CardNumber, 4, 2027, "EUR", 2500, Cvv, new DateOnly(2026, 9, 26)).Request!;

    [Theory]
    [InlineData(PaymentStatus.Authorized)]
    [InlineData(PaymentStatus.Declined)]
    public void Create_FromValidRequest_CopiesSafeFields(PaymentStatus status)
    {
        // Arrange

        // Act
        Payment payment = Payment.Create(Request, status);

        // Assert
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(status, payment.Status);
        Assert.Equal("0012", payment.CardNumberLastFour);
        Assert.Equal(4, payment.ExpiryMonth);
        Assert.Equal(2027, payment.ExpiryYear);
        Assert.Equal("EUR", payment.Currency);
        Assert.Equal(2500, payment.Amount);
    }

    [Fact]
    public void Create_Twice_AssignsDifferentIds()
    {
        // Arrange
        Payment first = Payment.Create(Request, PaymentStatus.Authorized);

        // Act
        Payment second = Payment.Create(Request, PaymentStatus.Authorized);

        // Assert
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void ToString_Always_ExcludesCardNumberAndCvv()
    {
        // Arrange
        Payment payment = Payment.Create(Request, PaymentStatus.Authorized);

        // Act
        string? text = payment.ToString();

        // Assert
        Assert.DoesNotContain(CardNumber, text);
        Assert.DoesNotContain(Cvv, text);
    }
}
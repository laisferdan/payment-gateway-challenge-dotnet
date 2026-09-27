using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Infrastructure;

namespace PaymentGateway.Api.Tests.Unit.Infrastructure;

public class BankPaymentRequestTests
{
    [Fact]
    public void ToString_Always_MasksCardNumberAndOmitsCvv()
    {
        // Arrange
        PaymentRequest request = PaymentRequest.Create(
            "2222405343248877", 4, 2027, "GBP", 100, "987", new DateOnly(2026, 9, 26)).Request!;
        BankPaymentRequest bankRequest = BankPaymentRequest.From(request);

        // Act
        string? text = bankRequest.ToString();

        // Assert
        Assert.Contains("************8877", text);
        Assert.DoesNotContain("2222405343248877", text);
        Assert.DoesNotContain("987", text);
    }
}
using PaymentGateway.Api.Http;

namespace PaymentGateway.Api.Tests.Unit.Http;

public class PostPaymentRequestTests
{
    [Fact]
    public void ToString_Always_MasksCardNumberAndOmitsCvv()
    {
        // Arrange
        PostPaymentRequest request = new()
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 100,
            Cvv = "987",
        };

        // Act
        string? text = request.ToString();

        // Assert
        Assert.Contains("************8877", text);
        Assert.DoesNotContain("2222405343248877", text);
        Assert.DoesNotContain("987", text);
    }
}
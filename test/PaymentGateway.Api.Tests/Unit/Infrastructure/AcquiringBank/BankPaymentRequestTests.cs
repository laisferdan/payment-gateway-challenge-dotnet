using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Infrastructure.AcquiringBank;
using PaymentGateway.Api.Tests.TestData;

namespace PaymentGateway.Api.Tests.Unit.Infrastructure.AcquiringBank;

public class BankPaymentRequestTests
{
    [Fact]
    public void ToString_Always_MasksCardNumberAndOmitsCvv()
    {
        // Arrange
        PaymentRequest request = ValidPaymentRequest.Create(cvv: "987");
        BankPaymentRequest bankRequest = BankPaymentRequest.From(request);

        // Act
        string? text = bankRequest.ToString();

        // Assert
        Assert.Contains("************8877", text);
        Assert.DoesNotContain("2222405343248877", text);
        Assert.DoesNotContain("987", text);
    }
}
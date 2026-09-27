using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Tests.Unit.Domain;

public class SupportedCurrenciesTests
{
    [Theory]
    [InlineData("GBP")]
    [InlineData("EUR")]
    [InlineData("USD")]
    public void IsSupported_ForSupportedCode_ReturnsTrue(string code)
    {
        // Arrange

        // Act
        bool supported = SupportedCurrencies.IsSupported(code);

        // Assert
        Assert.True(supported);
    }

    [Theory]
    [InlineData("gbp")]
    [InlineData("Gbp")]
    [InlineData("GB")]
    [InlineData("GBPX")]
    [InlineData("JPY")]
    [InlineData("")]
    public void IsSupported_ForUnsupportedCode_ReturnsFalse(string code)
    {
        // Arrange

        // Act
        bool supported = SupportedCurrencies.IsSupported(code);

        // Assert
        Assert.False(supported);
    }

    [Fact]
    public void Length_Always_IsThree()
    {
        // Arrange

        // Act
        int length = SupportedCurrencies.Length;

        // Assert
        Assert.Equal(3, length);
    }
}

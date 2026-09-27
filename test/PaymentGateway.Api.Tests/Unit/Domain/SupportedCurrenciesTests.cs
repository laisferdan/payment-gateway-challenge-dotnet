using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Tests.Unit.Domain;

public class SupportedCurrenciesTests
{
    [Theory]
    [InlineData("GBP", true)]
    [InlineData("EUR", true)]
    [InlineData("USD", true)]
    [InlineData("gbp", false)]
    [InlineData("Gbp", false)]
    [InlineData("GB", false)]
    [InlineData("GBPX", false)]
    [InlineData("JPY", false)]
    [InlineData("", false)]
    public void IsSupported_ForCode_ReturnsWhetherItIsInTheOrdinalList(string code, bool expected)
    {
        // Arrange – no setup: the list is fixed.

        // Act
        bool supported = SupportedCurrencies.IsSupported(code);

        // Assert
        Assert.Equal(expected, supported);
    }

    [Fact]
    public void Length_Always_IsThree()
    {
        // Arrange – no setup: the length is a constant.

        // Act
        int length = SupportedCurrencies.Length;

        // Assert
        Assert.Equal(3, length);
    }
}
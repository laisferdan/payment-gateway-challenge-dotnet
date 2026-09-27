using Microsoft.Extensions.Options;

using PaymentGateway.Api.Tests.Integration.Fixtures;

namespace PaymentGateway.Api.Tests.Integration;

public class StartupValidationTests
{
    private const string ValidBaseUrl = "http://localhost:8080";

    [Theory]
    [InlineData("", "10")]
    [InlineData("not-a-url", "10")]
    [InlineData(ValidBaseUrl, "0")]
    [InlineData(ValidBaseUrl, "61")]
    public void Startup_WhenAcquiringBankOptionsAreInvalid_FailsFast(string baseUrl, string timeoutSeconds)
    {
        // Arrange
        using PaymentGatewayFactory factory = new(baseUrl, new Dictionary<string, string?>
        {
            ["AcquiringBank:TimeoutSeconds"] = timeoutSeconds,
        });

        // Act
        Exception? exception = Record.Exception(() => factory.CreateClient());

        // Assert
        Assert.IsType<OptionsValidationException>(exception);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("60")]
    public void Startup_WhenOptionsAreValid_Starts(string timeoutSeconds)
    {
        // Arrange
        using PaymentGatewayFactory factory = new(ValidBaseUrl, new Dictionary<string, string?>
        {
            ["AcquiringBank:TimeoutSeconds"] = timeoutSeconds,
        });

        // Act
        Exception? exception = Record.Exception(() => factory.CreateClient());

        // Assert
        Assert.Null(exception);
    }
}
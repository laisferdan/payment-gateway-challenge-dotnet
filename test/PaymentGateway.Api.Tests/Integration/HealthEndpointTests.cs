using System.Net;

using PaymentGateway.Api.Tests.Integration.Fixtures;

namespace PaymentGateway.Api.Tests.Integration;

public class HealthEndpointTests : IClassFixture<WireMockBankFixture>
{
    private readonly WireMockBankFixture _bank;

    public HealthEndpointTests(WireMockBankFixture bank)
    {
        _bank = bank;
        _bank.Reset();
    }

    [Fact]
    public async Task Health_WhenGatewayIsRunning_Returns200Healthy()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
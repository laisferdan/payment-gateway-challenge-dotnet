using System.Net;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Api.Tests.EndToEnd;

/// <summary>
/// Merchant journeys against the real bank simulator. Excluded from the default run:
/// <c>docker compose up -d bank_simulator</c> then <c>dotnet test --filter "Category=E2E"</c>.
/// </summary>
[Trait("Category", "E2E")]
public class ProcessPaymentJourneyTests : IClassFixture<SimulatorGatewayFactory>
{
    private readonly SimulatorGatewayFactory _factory;

    public ProcessPaymentJourneyTests(SimulatorGatewayFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("2222405343248877", "Authorized")]
    [InlineData("2222405343248878", "Declined")]
    public async Task ProcessPayment_WhenSimulatorDecides_Returns200WithItsDecision(string cardNumber, string expectedStatus)
    {
        // Arrange – the simulator authorizes cards ending in an odd digit and declines even ones.
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await PostAsync(client, cardNumber);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(expectedStatus, body.GetProperty("status").GetString());
        Assert.Equal(cardNumber[^4..], body.GetProperty("cardNumberLastFour").GetString());
    }

    [Fact]
    public async Task ProcessPayment_WhenSimulatorIsUnavailable_Returns503BankUnavailable()
    {
        // Arrange – the simulator answers 503 for cards ending in 0.
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await PostAsync(client, "2222405343248870");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("bank_unavailable", body.GetProperty("errorCode").GetString());
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string cardNumber)
    {
        string json = $$"""{"cardNumber":"{{cardNumber}}","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":1050,"cvv":"123"}""";
        return client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));
    }
}
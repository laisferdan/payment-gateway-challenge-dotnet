using System.Net;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Api.Tests.EndToEnd;

[Trait("Category", "E2E")]
public class ProcessPaymentJourneyTests : IClassFixture<SimulatorGatewayFactory>
{
    private readonly SimulatorGatewayFactory _factory;

    public ProcessPaymentJourneyTests(SimulatorGatewayFactory factory)
    {
        _factory = factory;
    }

    [SimulatorTheory]
    [InlineData("2222405343248877", "Authorized")]
    [InlineData("2222405343248878", "Declined")]
    public async Task ProcessPayment_WhenSimulatorDecides_Returns201WithItsDecision(string cardNumber, string expectedStatus)
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await PostAsync(client, cardNumber);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(expectedStatus, body.GetProperty("status").GetString());
        Assert.Equal(cardNumber[^4..], body.GetProperty("cardNumberLastFour").GetString());
    }

    [SimulatorFact]
    public async Task ProcessPayment_WhenSimulatorIsUnavailable_Returns503BankUnavailable()
    {
        // Arrange
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
        // The real simulator runs on the real clock, so the expiry is always next year.
        int expiryYear = DateTime.UtcNow.Year + 1;
        string json = $$"""{"cardNumber":"{{cardNumber}}","expiryMonth":12,"expiryYear":{{expiryYear}},"currency":"GBP","amount":1050,"cvv":"123"}""";
        return client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));
    }
}
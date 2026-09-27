using System.Net;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Api.Tests.EndToEnd;

[Trait("Category", "E2E")]
public class RetrievePaymentJourneyTests : IClassFixture<SimulatorGatewayFactory>
{
    private readonly SimulatorGatewayFactory _factory;

    public RetrievePaymentJourneyTests(SimulatorGatewayFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("2222405343248877", "Authorized")]
    [InlineData("2222405343248878", "Declined")]
    public async Task ProcessThenRetrieve_AgainstTheRealSimulator_ReturnsTheSameDetails(string cardNumber, string expectedStatus)
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage postResponse = await PostAsync(client, cardNumber);
        string postText = await postResponse.Content.ReadAsStringAsync();
        string id = JsonDocument.Parse(postText).RootElement.GetProperty("id").GetString()!;

        // Act
        HttpResponseMessage getResponse = await client.GetAsync($"/api/payments/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        string getText = await getResponse.Content.ReadAsStringAsync();
        Assert.Equal(postText, getText);
        Assert.Equal(expectedStatus, JsonDocument.Parse(getText).RootElement.GetProperty("status").GetString());
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string cardNumber)
    {
        string json = $$"""{"cardNumber":"{{cardNumber}}","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":1050,"cvv":"123"}""";
        return client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));
    }
}
using System.Net;
using System.Text.Json;

namespace PaymentGateway.Api.Tests.EndToEnd;

[Trait("Category", "E2E")]
[Collection(BankSimulatorCollection.Name)]
public class RetrievePaymentJourneyTests
{
    private readonly SimulatorGatewayFactory _factory;

    public RetrievePaymentJourneyTests(SimulatorGatewayFactory factory)
    {
        _factory = factory;
    }

    [SimulatorTheory]
    [InlineData(PaymentJson.AuthorizedCard, "Authorized")]
    [InlineData(PaymentJson.DeclinedCard, "Declined")]
    public async Task ProcessThenRetrieve_AgainstTheRealSimulator_ReturnsTheSameDetailsWithoutCallingTheBank(string cardNumber, string expectedStatus)
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage postResponse = await client.PostAsync("/api/payments", PaymentJson.Create(PaymentJson.Card(cardNumber)));
        string postText = await postResponse.Content.ReadAsStringAsync();
        int bankRequestsBefore = await BankSimulator.CountRequestsAsync();

        // Act
        HttpResponseMessage getResponse = await client.GetAsync(postResponse.Headers.Location);

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        string getText = await getResponse.Content.ReadAsStringAsync();
        Assert.Equal(postText, getText);
        Assert.Equal(expectedStatus, JsonDocument.Parse(getText).RootElement.GetProperty("status").GetString());
        Assert.Equal(bankRequestsBefore, await BankSimulator.CountRequestsAsync());
    }

    [SimulatorFact]
    public async Task Retrieve_WhenNoPaymentHasTheId_Returns404()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Payment not found", body.GetProperty("title").GetString());
    }

    [SimulatorFact]
    public async Task Retrieve_WhenIdIsNotAGuid_Returns404()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/payments/not-a-guid");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
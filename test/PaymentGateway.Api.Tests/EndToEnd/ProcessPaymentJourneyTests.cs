using System.Net;
using System.Text.Json;

namespace PaymentGateway.Api.Tests.EndToEnd;

[Trait("Category", "E2E")]
[Collection(BankSimulatorCollection.Name)]
public class ProcessPaymentJourneyTests
{
    private readonly SimulatorGatewayFactory _factory;

    public ProcessPaymentJourneyTests(SimulatorGatewayFactory factory)
    {
        _factory = factory;
    }

    [SimulatorTheory]
    [InlineData(PaymentJson.AuthorizedCard, "Authorized")]
    [InlineData(PaymentJson.DeclinedCard, "Declined")]
    public async Task ProcessPayment_WhenSimulatorDecides_Returns201WithItsDecision(string cardNumber, string expectedStatus)
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        int bankRequestsBefore = await BankSimulator.CountRequestsAsync();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/payments", PaymentJson.Create(PaymentJson.Card(cardNumber)));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        string text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(cardNumber, text);
        Assert.DoesNotContain("\"cvv\"", text);
        JsonElement body = JsonDocument.Parse(text).RootElement;
        Assert.Equal(expectedStatus, body.GetProperty("status").GetString());
        Assert.Equal(cardNumber[^4..], body.GetProperty("cardNumberLastFour").GetString());
        Assert.EndsWith($"/api/payments/{body.GetProperty("id").GetString()}", response.Headers.Location!.ToString());
        Assert.True(response.Headers.Contains(Program.TraceIdHeader));
        Assert.Equal(bankRequestsBefore + 1, await BankSimulator.CountRequestsAsync());
    }

    [SimulatorFact]
    public async Task ProcessPayment_WhenSimulatorIsUnavailable_Returns503AndDoesNotRetry()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        int bankRequestsBefore = await BankSimulator.CountRequestsAsync();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/payments", PaymentJson.Create(PaymentJson.Card(PaymentJson.UnavailableCard)));

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("bank_unavailable", body.GetProperty("errorCode").GetString());
        Assert.False(body.TryGetProperty("attemptId", out _));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.Equal(bankRequestsBefore + 1, await BankSimulator.CountRequestsAsync());
    }

    [SimulatorTheory]
    [InlineData("cardNumber", "\"22224053432481\"")]
    [InlineData("cardNumber", "\"2222405343248877111\"")]
    [InlineData("expiryMonth", "1")]
    [InlineData("currency", "\"EUR\"")]
    [InlineData("currency", "\"USD\"")]
    [InlineData("amount", "1")]
    [InlineData("amount", "2147483647")]
    [InlineData("cvv", "\"1234\"")]
    [InlineData("cvv", "\"012\"")]
    public async Task ProcessPayment_WithBoundaryValue_IsSentToTheBankAndAuthorized(string field, string rawValue)
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        int bankRequestsBefore = await BankSimulator.CountRequestsAsync();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/payments", PaymentJson.Create((field, rawValue)));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Authorized", body.GetProperty("status").GetString());
        if (body.TryGetProperty(field, out JsonElement returned))
        {
            Assert.Equal(rawValue, returned.GetRawText());
        }

        Assert.Equal(bankRequestsBefore + 1, await BankSimulator.CountRequestsAsync());
    }

    [SimulatorTheory]
    [InlineData(0)]
    [InlineData(20)]
    public async Task ProcessPayment_WhenExpiryIsAtABoundary_IsAuthorized(int yearsAhead)
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/payments", PaymentJson.Create(PaymentJson.Expiry(DateTime.UtcNow.AddYears(yearsAhead))));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Authorized", body.GetProperty("status").GetString());
    }

    [SimulatorFact]
    public async Task ProcessPayment_WhenSameRequestIsSentTwice_CreatesTwoPayments()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        int bankRequestsBefore = await BankSimulator.CountRequestsAsync();

        // Act
        HttpResponseMessage first = await client.PostAsync("/api/payments", PaymentJson.Create());
        HttpResponseMessage second = await client.PostAsync("/api/payments", PaymentJson.Create());

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.NotEqual(first.Headers.Location, second.Headers.Location);
        Assert.Equal(bankRequestsBefore + 2, await BankSimulator.CountRequestsAsync());
    }
}
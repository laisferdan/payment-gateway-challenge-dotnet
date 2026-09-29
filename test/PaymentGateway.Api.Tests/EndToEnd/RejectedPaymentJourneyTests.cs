using System.Net;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Api.Tests.EndToEnd;

[Trait("Category", "E2E")]
[Collection(BankSimulatorCollection.Name)]
public class RejectedPaymentJourneyTests
{
    private readonly SimulatorGatewayFactory _factory;

    public RejectedPaymentJourneyTests(SimulatorGatewayFactory factory)
    {
        _factory = factory;
    }

    [SimulatorTheory]
    [InlineData("cardNumber", "\"2222405343248\"")]
    [InlineData("cardNumber", "\"22224053432488771111\"")]
    [InlineData("cardNumber", "\"2222 4053 4324 8877\"")]
    [InlineData("cardNumber", "\"222240534324887A\"")]
    [InlineData("cardNumber", null)]
    [InlineData("expiryMonth", "0")]
    [InlineData("expiryMonth", "13")]
    [InlineData("expiryYear", "2020")]
    [InlineData("currency", "\"gbp\"")]
    [InlineData("currency", "\"JPY\"")]
    [InlineData("amount", "0")]
    [InlineData("amount", "-1")]
    [InlineData("amount", "\"1050\"")]
    [InlineData("amount", "10.5")]
    [InlineData("amount", "2147483648")]
    [InlineData("cvv", "\"12\"")]
    [InlineData("cvv", "\"12345\"")]
    [InlineData("cvv", "123")]
    public async Task ProcessPayment_WithInvalidField_IsRejectedWithoutCallingTheBank(string field, string? rawValue)
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        int bankRequestsBefore = await BankSimulator.CountRequestsAsync();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/payments", PaymentJson.Create((field, rawValue)));

        // Assert
        JsonElement errors = await AssertRejectedAsync(response);
        Assert.True(errors.TryGetProperty(field, out _), $"No error for {field}: {errors}");
        Assert.Equal(bankRequestsBefore, await BankSimulator.CountRequestsAsync());
    }

    [SimulatorTheory]
    [InlineData(-1)]
    [InlineData(21 * 12)]
    public async Task ProcessPayment_WhenExpiryIsOutOfRange_IsRejectedWithoutCallingTheBank(int monthsAhead)
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        int bankRequestsBefore = await BankSimulator.CountRequestsAsync();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/payments", PaymentJson.Create(PaymentJson.Expiry(DateTime.UtcNow.AddMonths(monthsAhead))));

        // Assert
        JsonElement errors = await AssertRejectedAsync(response);
        Assert.True(errors.TryGetProperty("expiryYear", out _), $"No error for expiryYear: {errors}");
        Assert.Equal(bankRequestsBefore, await BankSimulator.CountRequestsAsync());
    }

    [SimulatorFact]
    public async Task ProcessPayment_WithSeveralInvalidFields_ReportsThemAllWithoutEchoingTheCard()
    {
        // Arrange
        const string invalidCard = "22224053432488";
        using HttpClient client = _factory.CreateClient();
        int bankRequestsBefore = await BankSimulator.CountRequestsAsync();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/payments", PaymentJson.Create(
            ("cardNumber", $"\"{invalidCard}A\""), ("currency", "\"JPY\""), ("amount", "0"), ("cvv", "\"12\"")));

        // Assert
        JsonElement errors = await AssertRejectedAsync(response);
        Assert.Equal(new[] { "amount", "cardNumber", "currency", "cvv" }, errors.EnumerateObject().Select(error => error.Name).Order());
        Assert.DoesNotContain(invalidCard, errors.GetRawText());
        Assert.Equal(bankRequestsBefore, await BankSimulator.CountRequestsAsync());
    }

    [SimulatorTheory]
    [InlineData("not json")]
    [InlineData("{")]
    [InlineData("[]")]
    public async Task ProcessPayment_WhenBodyCannotBeRead_IsRejectedWithoutCallingTheBank(string json)
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        int bankRequestsBefore = await BankSimulator.CountRequestsAsync();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        JsonElement errors = await AssertRejectedAsync(response);
        Assert.True(errors.TryGetProperty("body", out _), $"No error for body: {errors}");
        Assert.Equal(bankRequestsBefore, await BankSimulator.CountRequestsAsync());
    }

    private static async Task<JsonElement> AssertRejectedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Rejected", body.GetProperty("paymentStatus").GetString());
        return body.GetProperty("errors");
    }
}
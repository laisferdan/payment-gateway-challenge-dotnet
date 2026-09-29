using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Tests.Integration.Fixtures;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace PaymentGateway.Api.Tests.Integration;

public class ProcessPaymentEndpointTests : IClassFixture<WireMockBankFixture>
{
    private const string CardNumber = "2222405343248877";
    private const string Cvv = "123";
    private const string AuthorizedBody = """{"authorized":true,"authorization_code":"0bb07405-6d44-4b50-a14f-7ae0beff13ad"}""";
    private const string UnavailableBody = """{"errorMessage":"unavailable"}""";
    private const string DeclinedBody = """{"authorized":false,"authorization_code":""}""";

    private readonly WireMockBankFixture _bank;

    public ProcessPaymentEndpointTests(WireMockBankFixture bank)
    {
        _bank = bank;
        _bank.Reset();
    }

    [Theory]
    [InlineData(AuthorizedBody, "Authorized")]
    [InlineData(DeclinedBody, "Declined")]
    public async Task Post_WhenBankDecides_Returns201WithPaymentSummaryAndLocation(string bankBody, string expectedStatus)
    {
        // Arrange
        StubBank(200, bankBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await PostAsync(client, PaymentJson(CardNumber));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        string text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(CardNumber, text);
        Assert.DoesNotContain("\"cvv\"", text);
        Assert.DoesNotContain("\"cardNumber\"", text);
        JsonElement body = JsonDocument.Parse(text).RootElement;
        Assert.True(Guid.TryParse(body.GetProperty("id").GetString(), out _));
        Assert.Equal(expectedStatus, body.GetProperty("status").GetString());
        Assert.Equal("8877", body.GetProperty("cardNumberLastFour").GetString());
        Assert.Equal(12, body.GetProperty("expiryMonth").GetInt32());
        Assert.Equal(2030, body.GetProperty("expiryYear").GetInt32());
        Assert.Equal("GBP", body.GetProperty("currency").GetString());
        Assert.Equal(1050, body.GetProperty("amount").GetInt32());
        Assert.EndsWith($"/api/payments/{body.GetProperty("id").GetString()}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Post_WhenLastFourHaveLeadingZeros_ReturnsThemAsText()
    {
        // Arrange
        StubBank(200, DeclinedBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await PostAsync(client, PaymentJson("2222405343240012"));

        // Assert
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("0012", body.GetProperty("cardNumberLastFour").GetString());
    }

    [Fact]
    public async Task Post_WhenBodyHasUnknownMember_IgnoresIt()
    {
        // Arrange
        StubBank(200, AuthorizedBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        string json = PaymentJson(CardNumber).Replace("\"cvv\"", "\"foo\":1,\"cvv\"");

        // Act
        HttpResponseMessage response = await PostAsync(client, json);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenFieldsAreInvalid_Returns400RejectedListingEveryFieldWithoutCallingBank()
    {
        // Arrange
        StubBank(200, AuthorizedBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await PostAsync(client, PaymentJson("1234", currency: "gbp", amount: 0));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        string text = await response.Content.ReadAsStringAsync();
        JsonElement body = JsonDocument.Parse(text).RootElement;
        Assert.False(body.TryGetProperty("type", out _));
        Assert.Equal("Payment rejected", body.GetProperty("title").GetString());
        Assert.Equal("Rejected", body.GetProperty("paymentStatus").GetString());
        Assert.Equal(
            ["amount", "cardNumber", "currency"],
            body.GetProperty("errors").EnumerateObject().Select(error => error.Name).Order());
        Assert.Matches("^[0-9a-f]{32}$", body.GetProperty("traceId").GetString());
        Assert.DoesNotContain("1234", body.GetProperty("errors").GetRawText());
        Assert.Empty(_bank.Server.LogEntries);
    }

    [Theory]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":"ten","cvv":"123"}""", "amount", nameof(PaymentRequest.Amount))]
    [InlineData("""{"cardNumber":2222405343248877,"expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":1050,"cvv":"123"}""", "cardNumber", nameof(PaymentRequest.CardNumber))]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":2147483648,"cvv":"123"}""", "amount", nameof(PaymentRequest.Amount))]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":12.5,"expiryYear":2030,"currency":"GBP","amount":1050,"cvv":"123"}""", "expiryMonth", nameof(PaymentRequest.ExpiryMonth))]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":"1050","cvv":"123"}""", "amount", nameof(PaymentRequest.Amount))]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":"12","expiryYear":2030,"currency":"GBP","amount":1050,"cvv":"123"}""", "expiryMonth", nameof(PaymentRequest.ExpiryMonth))]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":12,"expiryYear":"2030","currency":"GBP","amount":1050,"cvv":"123"}""", "expiryYear", nameof(PaymentRequest.ExpiryYear))]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":12,"expiryYear":2030,"currency":"GBP","AMOUNT":"ten","cvv":"123"}""", "AMOUNT", nameof(PaymentRequest.Amount))]
    [InlineData("{", "body", null)]
    [InlineData("", "body", null)]
    public async Task Post_WhenBodyIsUnreadable_ReturnsRejectedWithTheFieldRule(string json, string expectedField, string? field)
    {
        // Arrange
        StubBank(200, AuthorizedBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await PostAsync(client, json);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Rejected", body.GetProperty("paymentStatus").GetString());
        Assert.Equal("Payment rejected", body.GetProperty("title").GetString());
        JsonProperty error = Assert.Single(body.GetProperty("errors").EnumerateObject());
        Assert.Equal(expectedField, error.Name);
        string expectedMessage = field is not null && PaymentRequest.Messages.ByField.TryGetValue(field, out string? message)
            ? message
            : "The request body must be a JSON object with the payment fields.";
        Assert.Equal(expectedMessage, Assert.Single(error.Value.EnumerateArray()).GetString());
        string messages = error.Value.GetRawText();
        Assert.DoesNotContain("2222405343248877", messages);
        Assert.DoesNotContain("ten", messages);
        Assert.DoesNotContain("2147483648", messages);
        Assert.Matches("^[0-9a-f]{32}$", body.GetProperty("traceId").GetString());
        Assert.Empty(_bank.Server.LogEntries);
    }

    [Theory]
    [InlineData(503, """{"errorMessage":"unavailable"}""", HttpStatusCode.ServiceUnavailable, "bank_unavailable", "Payment could not be processed")]
    [InlineData(400, """{"errorMessage":"missing field"}""", HttpStatusCode.BadGateway, "bank_error", "Payment could not be processed")]
    [InlineData(200, "not json", HttpStatusCode.GatewayTimeout, "bank_outcome_unknown", "Payment could not be confirmed")]
    public async Task Post_WhenBankFails_ReturnsClassifiedFailureWithoutPaymentStatus(
        int bankStatus, string bankBody, HttpStatusCode expectedStatus, string expectedErrorCode, string expectedTitle)
    {
        // Arrange
        StubBank(bankStatus, bankBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await PostAsync(client, PaymentJson(CardNumber));

        // Assert
        await AssertBankFailureAsync(response, expectedStatus, expectedErrorCode, expectedTitle);
        Assert.Single(_bank.Server.LogEntries);
    }

    [Fact]
    public async Task Post_WhenProcessed_ReturnsTheTraceIdInAHeaderAndPropagatesItToTheBank()
    {
        // Arrange
        StubBank(200, AuthorizedBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await PostAsync(client, PaymentJson(CardNumber));

        // Assert
        string traceId = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        Assert.Matches("^[0-9a-f]{32}$", traceId);
        string traceparent = Assert.Single(_bank.Server.LogEntries).RequestMessage!.Headers!["traceparent"].Single();
        Assert.Equal(traceId, traceparent.Split('-')[1]);
    }

    [Theory]
    [InlineData(200, "1234", "PaymentRejected")]
    [InlineData(503, CardNumber, "BankCallFailed")]
    public async Task Post_WhenOutcomeIsAnError_TraceIdMatchesTheOutcomeLogEntry(int bankStatus, string cardNumber, string eventName)
    {
        // Arrange
        StubBank(bankStatus, bankStatus == 200 ? AuthorizedBody : UnavailableBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await PostAsync(client, PaymentJson(cardNumber));

        // Assert
        string? traceId = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("traceId").GetString();
        FakeLogRecord entry = Assert.Single(factory.LogCollector.GetSnapshot(), record => record.Id.Name == eventName);
        Assert.Equal(traceId, LogText.TraceId(entry));
    }

    [Theory]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":"ten","cvv":"123"}""", "amount")]
    [InlineData("{", "body")]
    public async Task Post_WhenBodyIsUnreadable_LogsPaymentRequestUnreadableOnce(string json, string expectedInvalidFields)
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await PostAsync(client, json);

        // Assert
        string? traceId = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("traceId").GetString();
        FakeLogRecord record = Assert.Single(factory.LogCollector.GetSnapshot(), r => r.Id.Name == "PaymentRequestUnreadable");
        Assert.Equal(1003, record.Id.Id);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(expectedInvalidFields, record.GetStructuredStateValue("invalidFields"));
        Assert.DoesNotContain("ten", LogText.Of(record));
        Assert.Equal(traceId, LogText.TraceId(record));
    }

    private static async Task AssertBankFailureAsync(
        HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedErrorCode, string expectedTitle = "Payment could not be processed")
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        string text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(CardNumber, text);
        JsonElement body = JsonDocument.Parse(text).RootElement;
        Assert.Equal(expectedErrorCode, body.GetProperty("errorCode").GetString());
        Assert.Equal(expectedTitle, body.GetProperty("title").GetString());
        Assert.False(body.TryGetProperty("paymentStatus", out _));
        Assert.Matches("^[0-9a-f]{32}$", body.GetProperty("traceId").GetString());

        if (expectedStatus == HttpStatusCode.GatewayTimeout)
        {
            Assert.True(Guid.TryParse(body.GetProperty("attemptId").GetString(), out _));
        }
        else
        {
            Assert.False(body.TryGetProperty("attemptId", out _));
        }
    }

    private static string PaymentJson(string cardNumber, string currency = "GBP", int amount = 1050)
    {
        return $$"""{"cardNumber":"{{cardNumber}}","expiryMonth":12,"expiryYear":2030,"currency":"{{currency}}","amount":{{amount}},"cvv":"{{Cvv}}"}""";
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string json)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/payments")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        return await client.SendAsync(request);
    }

    private static async Task<string> PostAndReadIdAsync(HttpClient client)
    {
        HttpResponseMessage response = await PostAsync(client, PaymentJson(CardNumber));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;
    }

    private void StubBank(int statusCode, string body)
    {
        _bank.Server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }
}
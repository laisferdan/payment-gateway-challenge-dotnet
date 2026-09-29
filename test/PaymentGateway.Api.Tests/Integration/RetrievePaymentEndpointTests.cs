using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Testing;

using PaymentGateway.Api.Tests.Integration.Fixtures;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace PaymentGateway.Api.Tests.Integration;

public class RetrievePaymentEndpointTests : IClassFixture<WireMockBankFixture>
{
    private const string CardNumber = "2222405343248877";
    private const string Cvv = "123";
    private const string AuthorizedBody = """{"authorized":true,"authorization_code":"0bb07405-6d44-4b50-a14f-7ae0beff13ad"}""";
    private const string DeclinedBody = """{"authorized":false,"authorization_code":""}""";

    private const string MerchantTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    private readonly WireMockBankFixture _bank;

    public RetrievePaymentEndpointTests(WireMockBankFixture bank)
    {
        _bank = bank;
        _bank.Reset();
    }

    [Theory]
    [InlineData(AuthorizedBody)]
    [InlineData(DeclinedBody)]
    public async Task Get_AfterProcessing_ReturnsTheSameBodyAsTheProcessingResponse(string bankBody)
    {
        // Arrange
        StubBank(200, bankBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        HttpResponseMessage postResponse = await PostAsync(client, PaymentJson(CardNumber));
        string postText = await postResponse.Content.ReadAsStringAsync();
        string id = JsonDocument.Parse(postText).RootElement.GetProperty("id").GetString()!;

        // Act
        HttpResponseMessage getResponse = await client.GetAsync($"/api/payments/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        string getText = await getResponse.Content.ReadAsStringAsync();
        Assert.Equal(postText, getText);
    }

    [Fact]
    public async Task Get_ForARecordedPayment_MakesNoRequestToTheBank()
    {
        // Arrange
        StubBank(200, AuthorizedBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        string id = await PostAndReadIdAsync(client, CardNumber);

        // Act
        await client.GetAsync($"/api/payments/{id}");
        await client.GetAsync($"/api/payments/{id}");

        // Assert
        Assert.Single(_bank.Server.LogEntries);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Get_WhenIdIsWellFormedButUnknown_Returns404(string unknownId)
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/payments/{unknownId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Payment not found", body.GetProperty("title").GetString());
        Assert.Equal("No payment exists with the given id.", body.GetProperty("detail").GetString());
        Assert.Matches("^[0-9a-f]{32}$", body.GetProperty("traceId").GetString());
        Assert.False(body.TryGetProperty("cardNumberLastFour", out _));
    }

    [Fact]
    public async Task Get_WhenPaymentExists_LogsPaymentRetrievedUnderTheRequestTraceId()
    {
        // Arrange
        StubBank(200, AuthorizedBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        string id = await PostAndReadIdAsync(client, CardNumber);
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/payments/{id}");
        request.Headers.Add("traceparent", $"00-{MerchantTraceId}-00f067aa0ba902b7-01");

        // Act
        await client.SendAsync(request);

        // Assert
        FakeLogRecord record = Assert.Single(factory.LogCollector.GetSnapshot(), r => r.Id.Name == "PaymentRetrieved");
        Assert.Equal(MerchantTraceId, LogText.TraceId(record));
    }

    [Fact]
    public async Task Get_WhenIdIsUnknown_TraceIdCorrelatesWithThePaymentNotFoundLogEntry()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        // Assert
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        string? traceId = body.GetProperty("traceId").GetString();
        FakeLogRecord record = Assert.Single(factory.LogCollector.GetSnapshot(), r => r.Id.Name == "PaymentNotFound");
        Assert.Matches("^[0-9a-f]{32}$", traceId);
        Assert.Equal(traceId, LogText.TraceId(record));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("9c858901-8a57-4791-81fe-4c455b099b")]
    [InlineData("9c858901-8a57-4791-81fe-4c455b099bc912")]
    [InlineData("9c858901-8a57-4791-81fe-4c455b099bg9")]
    [InlineData("9c8589018a57-4791-81fe-4c455b099bc9")]
    [InlineData("{9c858901-8a57-4791-81fe-4c455b099bc9")]
    [InlineData("   ")]
    [InlineData("4111111111111111")]
    public async Task Get_WhenIdIsNotAGuid_Returns404WithoutEchoingIt(string invalidId)
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/payments/{Uri.EscapeDataString(invalidId)}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        string text = await response.Content.ReadAsStringAsync();
        JsonElement body = JsonDocument.Parse(text).RootElement;
        string traceId = body.GetProperty("traceId").GetString()!;
        Assert.Matches("^[0-9a-f]{32}$", traceId);

        // Excludes the traceId itself: it's random hex, so a short invalidId (e.g. "abc") can
        // coincidentally appear inside it without ever being echoed from the request.
        Assert.DoesNotContain(invalidId, text.Replace(traceId, string.Empty));
    }

    [Fact]
    public async Task Get_WhenIdIsNotAGuid_NeverReachesTheUseCase()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        await client.GetAsync("/api/payments/abc");

        // Assert
        Assert.DoesNotContain(factory.LogCollector.GetSnapshot(), r => r.Id.Name is "PaymentRetrieved" or "PaymentNotFound");
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public async Task Get_OnTheCollectionRoute_Returns405(string suffix)
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/payments{suffix}");

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Matches("^[0-9a-f]{32}$", body.GetProperty("traceId").GetString());
    }

    private static string PaymentJson(string cardNumber, string currency = "GBP", int amount = 1050)
    {
        return $$"""{"cardNumber":"{{cardNumber}}","expiryMonth":12,"expiryYear":2030,"currency":"{{currency}}","amount":{{amount}},"cvv":"{{Cvv}}"}""";
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string json)
    {
        return client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));
    }

    private static async Task<string> PostAndReadIdAsync(HttpClient client, string cardNumber)
    {
        HttpResponseMessage response = await PostAsync(client, PaymentJson(cardNumber));
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
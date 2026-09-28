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
    public async Task Get_WhenLastFourHaveLeadingZeros_ReturnsThemAsText()
    {
        // Arrange
        StubBank(200, DeclinedBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        string id = await PostAndReadIdAsync(client, "2222405343240012");

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/payments/{id}");

        // Assert
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("0012", body.GetProperty("cardNumberLastFour").GetString());
    }

    [Theory]
    [MemberData(nameof(AcceptedNotations))]
    public async Task Get_WithAnyAcceptedGuidNotation_ReturnsTheSamePayment(Func<Guid, string> notation)
    {
        // Arrange
        StubBank(200, AuthorizedBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        string id = await PostAndReadIdAsync(client, CardNumber);
        string requestPath = notation(Guid.Parse(id));

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/payments/{requestPath}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(id, body.GetProperty("id").GetString());
    }

    public static TheoryData<Func<Guid, string>> AcceptedNotations => new()
    {
        (Guid guid) => guid.ToString("D").ToUpperInvariant(),
        (Guid guid) => guid.ToString("N"),
        (Guid guid) => Uri.EscapeDataString(guid.ToString("B")),
        (Guid guid) => guid.ToString("P"),
    };

    [Fact]
    public async Task Get_CalledTwice_ReturnsTwoIdenticalBodies()
    {
        // Arrange
        StubBank(200, AuthorizedBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        string id = await PostAndReadIdAsync(client, CardNumber);

        // Act
        HttpResponseMessage first = await client.GetAsync($"/api/payments/{id}");
        HttpResponseMessage second = await client.GetAsync($"/api/payments/{id}");

        // Assert
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
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
    public async Task Get_WhenIdIsUnknown_ResponseIsIdenticalForEveryId()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage first = await client.GetAsync($"/api/payments/{Guid.NewGuid()}");
        HttpResponseMessage second = await client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        // Assert
        JsonElement firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement;
        JsonElement secondBody = JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(firstBody.GetProperty("title").GetString(), secondBody.GetProperty("title").GetString());
        Assert.Equal(firstBody.GetProperty("detail").GetString(), secondBody.GetProperty("detail").GetString());
        Assert.Equal(firstBody.GetProperty("status").GetInt32(), secondBody.GetProperty("status").GetInt32());
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
        string traceId = body.GetProperty("traceId").GetString()!;
        FakeLogRecord record = Assert.Single(factory.LogCollector.GetSnapshot(), r => r.Id.Name == "PaymentNotFound");
        Assert.Equal(traceId, LogText.TraceId(record));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("9c858901-8a57-4791-81fe-4c455b099b")] // 35 chars
    [InlineData("9c858901-8a57-4791-81fe-4c455b099bc912")] // 37 chars
    [InlineData("9c858901-8a57-4791-81fe-4c455b099bg9")] // non-hex 'g'
    [InlineData("9c8589018a57-4791-81fe-4c455b099bc9")] // misplaced hyphen
    [InlineData("{9c858901-8a57-4791-81fe-4c455b099bc9")] // mismatched bracket
    [InlineData(" 9c858901-8a57-4791-81fe-4c455b099bc9 ")] // surrounding spaces – never trimmed into validity
    [InlineData("9c858901-8a57-4791-81fe-4c455b099bc9 ")] // trailing space
    [InlineData("\t9c858901-8a57-4791-81fe-4c455b099bc9")] // leading tab
    [InlineData("4111111111111111")] // card-like
    public async Task Get_WhenIdIsNotAGuid_Returns400NamingTheIdField(string invalidId)
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/payments/{Uri.EscapeDataString(invalidId)}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        string text = await response.Content.ReadAsStringAsync();
        JsonElement body = JsonDocument.Parse(text).RootElement;
        Assert.Equal("Invalid payment id", body.GetProperty("title").GetString());
        string[] idErrors = body.GetProperty("errors").GetProperty("id").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(["The payment id must be a GUID, e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6."], idErrors);
        Assert.False(body.TryGetProperty("paymentStatus", out _));
        Assert.Matches("^[0-9a-f]{32}$", body.GetProperty("traceId").GetString());
        Assert.DoesNotContain(invalidId, text);
    }

    [Fact]
    public async Task Get_WhenIdIsWhitespaceOnly_Returns400()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/payments/{Uri.EscapeDataString("   ")}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Invalid payment id", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Get_WhenIdIsInvalid_LogsPaymentIdInvalidWithoutTheRawValue()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        await client.GetAsync("/api/payments/abc");

        // Assert
        FakeLogRecord record = Assert.Single(factory.LogCollector.GetSnapshot(), r => r.Id.Name == "PaymentIdInvalid");
        Assert.Equal(3002, record.Id.Id);
        Assert.DoesNotContain("abc", LogText.Of(record));
    }

    [Fact]
    public async Task Get_WhenIdIsInvalid_NeitherPaymentRetrievedNorPaymentNotFoundIsLogged()
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
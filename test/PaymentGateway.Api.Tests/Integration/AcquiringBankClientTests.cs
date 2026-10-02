using System.Net;
using System.Net.Sockets;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Infrastructure.AcquiringBank;
using PaymentGateway.Api.Tests.Integration.Fixtures;
using PaymentGateway.Api.Tests.TestData;

using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace PaymentGateway.Api.Tests.Integration;

public class AcquiringBankClientTests : IClassFixture<WireMockBankFixture>
{
    private static readonly PaymentRequest ValidRequest = ValidPaymentRequest.Create();
    private static readonly Guid PaymentId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    private readonly WireMockBankFixture _bank;

    public AcquiringBankClientTests(WireMockBankFixture bank)
    {
        _bank = bank;
        _bank.Reset();
    }

    [Fact]
    public async Task RequestAuthorization_WhenBankAuthorizes_ReturnsItsAuthorizationCode()
    {
        // Arrange
        StubBank(200, """{"authorized":true,"authorization_code":"0bb07405-6d44-4b50-a14f-7ae0beff13ad"}""");
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        Assert.Equal("0bb07405-6d44-4b50-a14f-7ae0beff13ad", Assert.IsType<BankAuthorizationResult.Authorized>(result).AuthorizationCode);
    }

    [Fact]
    public async Task RequestAuthorization_WhenBankDeclines_ReturnsDeclined()
    {
        // Arrange
        StubBank(200, """{"authorized":false,"authorization_code":""}""");
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        Assert.IsType<BankAuthorizationResult.Declined>(result);
    }

    [Fact]
    public async Task RequestAuthorization_Always_SendsTheBankContractOnce()
    {
        // Arrange
        StubBank(200, """{"authorized":true,"authorization_code":"abc"}""");
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        IRequestMessage message = Assert.Single(_bank.Server.LogEntries).RequestMessage!;
        Assert.Equal("POST", message.Method);
        Assert.Equal("/payments", message.Path);
        JsonElement body = JsonDocument.Parse(message.Body!).RootElement;
        Assert.Equal("2222405343248877", body.GetProperty("card_number").GetString());
        Assert.Equal("04/2027", body.GetProperty("expiry_date").GetString());
        Assert.Equal("GBP", body.GetProperty("currency").GetString());
        Assert.Equal(100, body.GetProperty("amount").GetInt32());
        Assert.Equal("123", body.GetProperty("cvv").GetString());
    }

    [Theory]
    [InlineData(503, """{"errorMessage":"unavailable"}""", BankFailureKind.Unavailable)]
    [InlineData(408, "", BankFailureKind.Unavailable)]
    [InlineData(429, "", BankFailureKind.Unavailable)]
    [InlineData(400, """{"errorMessage":"missing field"}""", BankFailureKind.Error)]
    [InlineData(404, "", BankFailureKind.Error)]
    [InlineData(422, "", BankFailureKind.Error)]
    [InlineData(201, "", BankFailureKind.OutcomeUnknown)]
    [InlineData(202, "", BankFailureKind.OutcomeUnknown)]
    [InlineData(302, "", BankFailureKind.OutcomeUnknown)]
    [InlineData(500, "", BankFailureKind.OutcomeUnknown)]
    [InlineData(502, "", BankFailureKind.OutcomeUnknown)]
    [InlineData(504, "", BankFailureKind.OutcomeUnknown)]
    [InlineData(200, "not json", BankFailureKind.OutcomeUnknown)]
    [InlineData(200, """{"authorization_code":"abc"}""", BankFailureKind.OutcomeUnknown)]
    [InlineData(200, """{"authorized":true,"authorization_code":""}""", BankFailureKind.OutcomeUnknown)]
    public async Task RequestAuthorization_WhenBankFails_ReturnsFailureAfterOneCall(int statusCode, string bankBody, BankFailureKind expected)
    {
        // Arrange
        StubBank(statusCode, bankBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        Assert.Equal(expected, Assert.IsType<BankAuthorizationResult.Failed>(result).Kind);
        Assert.Single(_bank.Server.LogEntries);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task RequestAuthorization_WhenBankRedirects_DoesNotFollowItAndReturnsOutcomeUnknown(int statusCode)
    {
        // Arrange: following a 307/308 would re-send the card number and CVV to wherever Location points.
        _bank.Server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode).WithHeader("Location", $"{_bank.Url}/elsewhere"));
        _bank.Server
            .Given(Request.Create().WithPath("/elsewhere"))
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"authorized":true,"authorization_code":"abc"}"""));
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        Assert.Equal(BankFailureKind.OutcomeUnknown, Assert.IsType<BankAuthorizationResult.Failed>(result).Kind);
        Assert.Equal("/payments", Assert.Single(_bank.Server.LogEntries).RequestMessage!.Path);
    }

    [Fact]
    public async Task RequestAuthorization_WhenA200HasAnUnknownCharset_ReturnsOutcomeUnknown()
    {
        // Arrange
        _bank.Server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json; charset=unknown")
                .WithBody("""{"authorized":true,"authorization_code":"abc"}"""));
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        Assert.Equal(BankFailureKind.OutcomeUnknown, Assert.IsType<BankAuthorizationResult.Failed>(result).Kind);
    }

    [Fact]
    public async Task RequestAuthorization_WhenBankDoesNotAnswerInTime_ReturnsOutcomeUnknown()
    {
        // Arrange
        using WireMockBankFixture slowBank = new();
        StubBank(slowBank, 200, """{"authorized":true,"authorization_code":"abc"}""", TimeSpan.FromSeconds(3));
        using PaymentGatewayFactory factory = new(slowBank.Url, PaymentGatewayFactory.ShortBankTimeout);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        Assert.Equal(BankFailureKind.OutcomeUnknown, Assert.IsType<BankAuthorizationResult.Failed>(result).Kind);
    }

    [Fact]
    public async Task RequestAuthorization_WhenConnectionDropsAfterTheRequestWasSent_ReturnsOutcomeUnknown()
    {
        // Arrange
        using TcpListener bank = new(IPAddress.Loopback, 0);
        bank.Start();
        Task hangUp = HangUpAfterReadingAsync(bank);
        using PaymentGatewayFactory factory = new($"http://127.0.0.1:{((IPEndPoint)bank.LocalEndpoint).Port}");
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        await hangUp;
        Assert.Equal(BankFailureKind.OutcomeUnknown, Assert.IsType<BankAuthorizationResult.Failed>(result).Kind);
    }

    private static async Task HangUpAfterReadingAsync(TcpListener listener)
    {
        using TcpClient connection = await listener.AcceptTcpClientAsync();
        await connection.GetStream().ReadAsync(new byte[4096]);
    }

    [Fact]
    public async Task RequestAuthorization_WhenBankCannotBeReached_ReturnsUnavailable()
    {
        // Arrange
        using PaymentGatewayFactory factory = new($"http://127.0.0.1:{UnusedPort()}");
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        Assert.Equal(BankFailureKind.Unavailable, Assert.IsType<BankAuthorizationResult.Failed>(result).Kind);
    }

    [Theory]
    [InlineData(503, "", BankFailureKind.Unavailable, "503", LogLevel.Warning, "UnexpectedStatus")]
    [InlineData(400, "", BankFailureKind.Error, "400", LogLevel.Warning, "UnexpectedStatus")]
    [InlineData(200, "not json", BankFailureKind.OutcomeUnknown, "200", LogLevel.Error, "UnreadableBody")]
    [InlineData(200, """{"authorized":true,"authorization_code":""}""", BankFailureKind.OutcomeUnknown, "200", LogLevel.Error, "IncompleteBody")]
    public async Task RequestAuthorization_WhenBankFails_LogsAtTheRightLevelWithTheHttpStatus(
        int statusCode, string bankBody, BankFailureKind expectedKind, string expectedHttpStatus, LogLevel expectedLevel, string expectedReason)
    {
        // Arrange
        StubBank(statusCode, bankBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        FakeLogRecord entry = Assert.Single(factory.LogCollector.GetSnapshot(), record => record.Category == typeof(AcquiringBankClient).FullName);
        Assert.Equal("BankCallFailed", entry.Id.Name);
        Assert.Equal(expectedLevel, entry.Level);
        Assert.Equal(PaymentId.ToString(), entry.GetStructuredStateValue("paymentId"));
        Assert.Equal("8877", entry.GetStructuredStateValue("cardNumberLastFour"));
        Assert.Equal(expectedKind.ToString(), entry.GetStructuredStateValue("failureKind"));
        Assert.Equal(expectedHttpStatus, entry.GetStructuredStateValue("httpStatusCode"));
        Assert.NotNull(entry.GetStructuredStateValue("elapsedMs"));
        Assert.Equal("GBP", entry.GetStructuredStateValue("currency"));
        Assert.Equal("100", entry.GetStructuredStateValue("amount"));
        Assert.Equal(expectedReason, entry.GetStructuredStateValue("failureReason"));
        Assert.DoesNotContain("2222405343248877", LogText.Of(entry));
    }

    [Fact]
    public async Task RequestAuthorization_WhenBankDoesNotAnswerInTime_LogsTimeoutWithTheException()
    {
        // Arrange
        using WireMockBankFixture slowBank = new();
        StubBank(slowBank, 200, """{"authorized":true,"authorization_code":"abc"}""", TimeSpan.FromSeconds(3));
        using PaymentGatewayFactory factory = new(slowBank.Url, PaymentGatewayFactory.ShortBankTimeout);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        FakeLogRecord entry = Assert.Single(factory.LogCollector.GetSnapshot(), record => record.Id.Name == "BankCallFailed");
        Assert.Equal("Timeout", entry.GetStructuredStateValue("failureReason"));
        Assert.IsAssignableFrom<OperationCanceledException>(entry.Exception);
    }

    [Fact]
    public async Task RequestAuthorization_WhenBankCannotBeReached_LogsTheConnectionErrorWithTheException()
    {
        // Arrange
        using PaymentGatewayFactory factory = new($"http://127.0.0.1:{UnusedPort()}");
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        FakeLogRecord entry = Assert.Single(factory.LogCollector.GetSnapshot(), record => record.Id.Name == "BankCallFailed");
        Assert.Equal(nameof(HttpRequestError.ConnectionError), entry.GetStructuredStateValue("failureReason"));
        Assert.IsType<HttpRequestException>(entry.Exception);
    }

    [Theory]
    [InlineData("""{"authorized":true,"authorization_code":"abc"}""", "Authorized")]
    [InlineData("""{"authorized":false,"authorization_code":""}""", "Declined")]
    public async Task RequestAuthorization_WhenBankDecides_LogsTheDecisionAndHowLongItTook(string bankBody, string expectedDecision)
    {
        // Arrange
        StubBank(200, bankBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        await client.RequestAuthorizationAsync(ValidRequest, PaymentId);

        // Assert
        FakeLogRecord entry = Assert.Single(factory.LogCollector.GetSnapshot(), record => record.Category == typeof(AcquiringBankClient).FullName);
        Assert.Equal("BankCallCompleted", entry.Id.Name);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(expectedDecision, entry.GetStructuredStateValue("decision"));
        Assert.True(long.Parse(entry.GetStructuredStateValue("elapsedMs")!) >= 0);
        Assert.DoesNotContain("2222405343248877", LogText.Of(entry));
    }

    private static int UnusedPort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private void StubBank(int statusCode, string body)
    {
        StubBank(_bank, statusCode, body, delay: null);
    }

    private static void StubBank(WireMockBankFixture bank, int statusCode, string body, TimeSpan? delay)
    {
        IResponseBuilder response = Response.Create()
            .WithStatusCode(statusCode)
            .WithHeader("Content-Type", "application/json")
            .WithBody(body);
        if (delay is TimeSpan wait)
        {
            response = response.WithDelay(wait);
        }

        bank.Server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(response);
    }
}
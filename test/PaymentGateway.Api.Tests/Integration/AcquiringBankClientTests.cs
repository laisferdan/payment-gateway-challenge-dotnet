using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using PaymentGateway.Api.Application;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Infrastructure;
using PaymentGateway.Api.Tests.Integration.Fixtures;

using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace PaymentGateway.Api.Tests.Integration;

/// <summary>
/// The real bank adapter, resolved from the application's services (real HttpClient setup and
/// options), against WireMock standing in for the bank simulator.
/// </summary>
public class AcquiringBankClientTests : IClassFixture<WireMockBankFixture>
{
    private static readonly PaymentRequest ValidRequest = PaymentRequest.Create(
        "2222405343248877", 4, 2027, "GBP", 100, "123", new DateOnly(2026, 9, 26)).Request!;

    private readonly WireMockBankFixture _bank;

    public AcquiringBankClientTests(WireMockBankFixture bank)
    {
        _bank = bank;
        _bank.Reset();
    }

    [Theory]
    [InlineData("""{"authorized":true,"authorization_code":"0bb07405-6d44-4b50-a14f-7ae0beff13ad"}""", typeof(BankAuthorizationResult.Authorized))]
    [InlineData("""{"authorized":false,"authorization_code":""}""", typeof(BankAuthorizationResult.Declined))]
    public async Task RequestAuthorization_WhenBankDecides_ReturnsDecision(string bankBody, Type expected)
    {
        // Arrange
        StubBank(200, bankBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, CancellationToken.None);

        // Assert
        Assert.IsType(expected, result);
    }

    [Fact]
    public async Task RequestAuthorization_Always_SendsTheBankContractOnce()
    {
        // Arrange
        StubBank(200, """{"authorized":true,"authorization_code":"abc"}""");
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        await client.RequestAuthorizationAsync(ValidRequest, CancellationToken.None);

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
    [InlineData(400, """{"errorMessage":"missing field"}""", BankFailureKind.Error)]
    [InlineData(500, "", BankFailureKind.Error)]
    [InlineData(200, "not json", BankFailureKind.Error)]
    [InlineData(200, """{"authorization_code":"abc"}""", BankFailureKind.Error)]
    [InlineData(200, """{"authorized":true,"authorization_code":""}""", BankFailureKind.Error)]
    public async Task RequestAuthorization_WhenBankFails_ReturnsFailureAfterOneCall(int statusCode, string bankBody, BankFailureKind expected)
    {
        // Arrange – 503; 400; another status; unreadable body; missing "authorized"; authorized without a code.
        StubBank(statusCode, bankBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<BankAuthorizationResult.Failed>(result).Kind);
        Assert.Single(_bank.Server.LogEntries);
    }

    [Fact]
    public async Task RequestAuthorization_WhenBankDoesNotAnswerInTime_ReturnsUnavailable()
    {
        // Arrange – the bank answers after 3 s; the timeout is 1 s. A server of its own: the late
        // request would otherwise be recorded in the shared server during the next test.
        using WireMockBankFixture slowBank = new();
        StubBank(slowBank, 200, """{"authorized":true,"authorization_code":"abc"}""", TimeSpan.FromSeconds(3));
        using PaymentGatewayFactory factory = new(slowBank.Url, PaymentGatewayFactory.ShortBankTimeout);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, CancellationToken.None);

        // Assert – WireMock records a request only once its delayed response completes, so the
        // single-call guarantee is asserted by the other failure cases, not here.
        Assert.Equal(BankFailureKind.Unavailable, Assert.IsType<BankAuthorizationResult.Failed>(result).Kind);
    }

    [Fact]
    public async Task RequestAuthorization_WhenBankCannotBeReached_ReturnsUnavailable()
    {
        // Arrange – an unused local port, so the shared WireMock server keeps running.
        using PaymentGatewayFactory factory = new($"http://127.0.0.1:{UnusedPort()}");
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();

        // Act
        BankAuthorizationResult result = await client.RequestAuthorizationAsync(ValidRequest, CancellationToken.None);

        // Assert
        Assert.Equal(BankFailureKind.Unavailable, Assert.IsType<BankAuthorizationResult.Failed>(result).Kind);
    }

    [Fact]
    public async Task RequestAuthorization_WhenCallerCancels_PropagatesCancellation()
    {
        // Arrange – the only case built directly: it needs a caller token, not the options.
        using WireMockBankFixture slowBank = new();
        StubBank(slowBank, 200, """{"authorized":true,"authorization_code":"abc"}""", TimeSpan.FromSeconds(3));
        using HttpClient httpClient = new() { BaseAddress = new Uri(slowBank.Url) };
        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        AcquiringBankClient client = new(
            httpClient,
            TimeProvider.System,
            new PaymentGatewayMetrics(services.GetRequiredService<IMeterFactory>()),
            new FakeLogger<AcquiringBankClient>());
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(200));

        // Act
        Exception? exception = await Record.ExceptionAsync(() => client.RequestAuthorizationAsync(ValidRequest, cancellation.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    [Theory]
    [InlineData(200, """{"authorized":true,"authorization_code":"abc"}""", "BankCallCompleted", LogLevel.Information, "authorized", null)]
    [InlineData(200, """{"authorized":false,"authorization_code":""}""", "BankCallCompleted", LogLevel.Information, "declined", null)]
    [InlineData(503, "", "BankCallFailed", LogLevel.Warning, "bank_unavailable", "503")]
    [InlineData(400, "", "BankCallFailed", LogLevel.Warning, "bank_error", "400")]
    public async Task RequestAuthorization_ForEachCall_LogsOneEntryAndRecordsDuration(
        int statusCode, string bankBody, string expectedEvent, LogLevel expectedLevel, string expectedOutcome, string? expectedHttpStatus)
    {
        // Arrange
        StubBank(statusCode, bankBody);
        using PaymentGatewayFactory factory = new(_bank.Url);
        IAcquiringBank client = factory.Services.GetRequiredService<IAcquiringBank>();
        using MetricCollector<double> duration = new(
            factory.Services.GetRequiredService<IMeterFactory>(), "PaymentGateway", "paymentgateway.bank.request.duration");

        // Act
        await client.RequestAuthorizationAsync(ValidRequest, CancellationToken.None);

        // Assert
        FakeLogRecord entry = Assert.Single(factory.LogCollector.GetSnapshot(), record => record.Category == typeof(AcquiringBankClient).FullName);
        Assert.Equal(expectedEvent, entry.Id.Name);
        Assert.Equal(expectedLevel, entry.Level);
        Assert.NotNull(entry.GetStructuredStateValue("durationMs"));
        Assert.Equal(expectedHttpStatus, entry.GetStructuredStateValue("httpStatusCode"));
        Assert.Equal(expectedOutcome, Assert.Single(duration.GetMeasurementSnapshot()).Tags["outcome"]);
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
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using PaymentGateway.Api.Tests.Integration.Fixtures;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace PaymentGateway.Api.Tests.Integration;

public class CardDataLoggingTests : IClassFixture<WireMockBankFixture>
{
    private const string CardNumber = "4111111111111111";
    private const string Cvv = "739";

    private readonly WireMockBankFixture _bank;

    public CardDataLoggingTests(WireMockBankFixture bank)
    {
        _bank = bank;
        _bank.Reset();
    }

    [Fact]
    public async Task RequestPath_WhenItContainsACardNumber_IsNotLogged()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        await client.GetAsync($"/api/payments/{CardNumber}");

        // Assert
        Assert.DoesNotContain(factory.LogCollector.GetSnapshot(), record => LogText.Of(record).Contains(CardNumber));
    }

    [Theory]
    [InlineData(200, """{"authorized":true,"authorization_code":"abc"}""", "GBP")]
    [InlineData(200, """{"authorized":true,"authorization_code":"abc"}""", "gbp")]
    [InlineData(503, "", "GBP")]
    public async Task RequestBody_WhenItContainsCardData_IsNotLogged(int bankStatus, string bankBody, string currency)
    {
        // Arrange
        _bank.Server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(bankStatus).WithHeader("Content-Type", "application/json").WithBody(bankBody));
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        string json = $$"""{"cardNumber":"{{CardNumber}}","expiryMonth":12,"expiryYear":2030,"currency":"{{currency}}","amount":1050,"cvv":"{{Cvv}}"}""";

        // Act
        await client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        Assert.NotEmpty(factory.LogCollector.GetSnapshot());
        Assert.DoesNotContain(factory.LogCollector.GetSnapshot(), record => LogText.Of(record).Contains(CardNumber) || LogText.Of(record).Contains(Cvv));
    }

    [Fact]
    public async Task UnreadableBody_WhenItContainsCardData_IsNotLogged()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        string json = $$"""{"cardNumber":"{{CardNumber}}","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":"ten","cvv":"{{Cvv}}"}""";

        // Act
        await client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        Assert.DoesNotContain(factory.LogCollector.GetSnapshot(), record => LogText.Of(record).Contains(CardNumber) || LogText.Of(record).Contains(Cvv));
    }

    [Fact]
    public async Task Get_ForAProcessedPayment_LogsNoPanOrCvv()
    {
        // Arrange
        _bank.Server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"authorized":true,"authorization_code":"abc"}"""));
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        string json = $$"""{"cardNumber":"{{CardNumber}}","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":1050,"cvv":"{{Cvv}}"}""";
        HttpResponseMessage postResponse = await client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));
        string id = System.Text.Json.JsonDocument.Parse(await postResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;

        // Act
        await client.GetAsync($"/api/payments/{id}");

        // Assert
        Assert.DoesNotContain(factory.LogCollector.GetSnapshot(), record => LogText.Of(record).Contains(CardNumber) || LogText.Of(record).Contains(Cvv));
    }

    [Fact]
    public async Task Get_WithACardLikeInvalidId_IsNotLoggedOrReturned()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/payments/{CardNumber}");

        // Assert
        string text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(CardNumber, text);
        Assert.DoesNotContain(factory.LogCollector.GetSnapshot(), record => LogText.Of(record).Contains(CardNumber));
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics")]
    [InlineData("Microsoft.AspNetCore.Routing")]
    [InlineData("System.Net.Http.HttpClient.IAcquiringBank.LogicalHandler")]
    [InlineData("System.Net.Http.HttpClient.IAcquiringBank.ClientHandler")]
    public void Logging_ForFrameworkRequestCategories_IsWarningOrAbove(string category)
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        ILoggerFactory loggerFactory = factory.Services.GetRequiredService<ILoggerFactory>();

        // Act
        ILogger logger = loggerFactory.CreateLogger(category);

        // Assert
        Assert.False(logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void Logging_ForHostingDiagnostics_IsOffSoNoRequestPathScopeIsCreated()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        ILoggerFactory loggerFactory = factory.Services.GetRequiredService<ILoggerFactory>();

        // Act
        ILogger logger = loggerFactory.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");

        // Assert
        Assert.False(logger.IsEnabled(LogLevel.Critical));
    }
}
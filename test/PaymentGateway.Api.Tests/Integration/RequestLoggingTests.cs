using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using PaymentGateway.Api.Tests.Integration.Fixtures;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace PaymentGateway.Api.Tests.Integration;

public class RequestLoggingTests : IClassFixture<WireMockBankFixture>
{
    private const string HttpLoggingCategory = "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware";

    private readonly WireMockBankFixture _bank;

    public RequestLoggingTests(WireMockBankFixture bank)
    {
        _bank = bank;
        _bank.Reset();
    }

    [Fact]
    public async Task Post_WhenHandled_LogsOneRequestEntryWithMethodStatusAndDurationButNoPath()
    {
        // Arrange
        _bank.Server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503).WithHeader("Content-Type", "application/json").WithBody("{}"));
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        const string json = """{"cardNumber":"2222405343248870","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":1050,"cvv":"123"}""";

        // Act
        await client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        FakeLogRecord entry = Assert.Single(factory.LogCollector.GetSnapshot(), record => record.Category == HttpLoggingCategory);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("POST", entry.GetStructuredStateValue("Method"));
        Assert.Equal("503", entry.GetStructuredStateValue("StatusCode"));
        Assert.NotNull(entry.GetStructuredStateValue("Duration"));
        Assert.Equal("api/payments", entry.GetStructuredStateValue("Route"));
        Assert.DoesNotContain("/api/payments", LogText.Of(entry));
    }

    [Fact]
    public async Task Get_WithACardNumberInThePath_LogsNoRouteAndNotThePath()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        await client.GetAsync("/api/payments/4111111111111111");

        // Assert
        // "4111111111111111" fails the {id:guid} constraint, so no endpoint matches and there is no
        // route template to log; the interceptor must still not fall back to logging the raw path.
        FakeLogRecord entry = Assert.Single(factory.LogCollector.GetSnapshot(), record => record.Category == HttpLoggingCategory);
        Assert.Equal("404", entry.GetStructuredStateValue("StatusCode"));
        Assert.Null(entry.GetStructuredStateValue("Route"));
        Assert.DoesNotContain("4111111111111111", LogText.Of(entry));
    }

    [Fact]
    public async Task Health_WhenProbed_IsNotLogged()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        await client.GetAsync("/health");

        // Assert
        Assert.DoesNotContain(factory.LogCollector.GetSnapshot(), record => record.Category == HttpLoggingCategory);
    }
}
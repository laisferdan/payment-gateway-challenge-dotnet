using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.Payments;
using PaymentGateway.Api.Tests.Integration.Fixtures;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace PaymentGateway.Api.Tests.Integration;

public class ErrorFormatTests : IClassFixture<WireMockBankFixture>
{
    private readonly WireMockBankFixture _bank;

    public ErrorFormatTests(WireMockBankFixture bank)
    {
        _bank = bank;
        _bank.Reset();
    }

    [Fact]
    public async Task UnknownRoute_WhenRequested_ReturnsProblemDetailsWithTraceId()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/unknown");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.False(body.TryGetProperty("type", out _));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("title").GetString()));
        Assert.Matches("^[0-9a-f]{32}$", body.GetProperty("traceId").GetString());
        Assert.Equal(body.GetProperty("traceId").GetString(), Assert.Single(response.Headers.GetValues("X-Trace-Id")));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task WrongMethod_OnPaymentsRoute_ReturnsProblemDetailsWithTraceId(string method)
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), "/api/payments")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Matches("^[0-9a-f]{32}$", body.GetProperty("traceId").GetString());
        Assert.False(body.TryGetProperty("paymentStatus", out _));
    }

    [Fact]
    public async Task UnexpectedError_WhenRepositoryThrows_Returns500WithoutDetails()
    {
        // Arrange
        _bank.Server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"authorized":true,"authorization_code":"abc"}"""));
        using PaymentGatewayFactory factory = new(_bank.Url, configureServices: services =>
        {
            services.RemoveAll<IPaymentRepository>();
            services.AddSingleton<IPaymentRepository, ThrowingPaymentRepository>();
        });
        using HttpClient client = factory.CreateClient();
        const string json = """{"cardNumber":"2222405343248877","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":1050,"cvv":"123"}""";

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        string text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ThrowingPaymentRepository.Secret, text);
        Assert.DoesNotContain("   at ", text);
        string? traceId = JsonDocument.Parse(text).RootElement.GetProperty("traceId").GetString();
        Assert.Matches("^[0-9a-f]{32}$", traceId);
        Assert.Equal(traceId, Assert.Single(response.Headers.GetValues("X-Trace-Id")));
    }

    private sealed class ThrowingPaymentRepository : IPaymentRepository
    {
        public const string Secret = "storage exploded";

        public Task AddAsync(Payment payment)
        {
            throw new InvalidOperationException(Secret);
        }

        public Task<Payment?> GetByIdAsync(Guid id)
        {
            throw new InvalidOperationException(Secret);
        }
    }
}
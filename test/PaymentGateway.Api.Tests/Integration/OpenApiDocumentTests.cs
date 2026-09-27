using System.Net;
using System.Text.Json;

using PaymentGateway.Api.Tests.Integration.Fixtures;

namespace PaymentGateway.Api.Tests.Integration;

public class OpenApiDocumentTests : IClassFixture<WireMockBankFixture>
{
    private const string DocumentPath = "/swagger/v1/swagger.json";

    private static readonly IDictionary<string, string?> SwaggerEnabled = new Dictionary<string, string?>
    {
        ["Swagger:Enabled"] = "true",
    };

    private readonly WireMockBankFixture _bank;

    public OpenApiDocumentTests(WireMockBankFixture bank)
    {
        _bank = bank;
        _bank.Reset();
    }

    [Fact]
    public async Task SwaggerDocument_WhenSwaggerIsEnabled_IsServed()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url, SwaggerEnabled);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(DocumentPath);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerDocument_ByDefault_IsNotServed()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(DocumentPath);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("200", "PaymentResponse")]
    [InlineData("400", "PaymentRejectedProblemDetails")]
    [InlineData("502", "BankFailureProblemDetails")]
    [InlineData("503", "BankFailureProblemDetails")]
    [InlineData("500", "ProblemDetails")]
    public async Task ProcessPayment_ForEachStatusCode_DocumentsItsBodySchema(string statusCode, string expectedSchema)
    {
        // Arrange
        JsonElement document = await GetDocumentAsync();

        // Act
        JsonElement response = document.GetProperty("paths").GetProperty("/api/payments").GetProperty("post")
            .GetProperty("responses").GetProperty(statusCode);

        // Assert
        JsonElement content = response.GetProperty("content").EnumerateObject().First().Value;
        Assert.Equal($"#/components/schemas/{expectedSchema}", content.GetProperty("schema").GetProperty("$ref").GetString());
    }

    [Theory]
    [InlineData("PaymentRejectedProblemDetails", "paymentStatus")]
    [InlineData("PaymentRejectedProblemDetails", "errors")]
    [InlineData("BankFailureProblemDetails", "errorCode")]
    public async Task ErrorSchemas_Always_ExposeTheirExtensionMembers(string schema, string property)
    {
        // Arrange
        JsonElement document = await GetDocumentAsync();

        // Act
        JsonElement properties = document.GetProperty("components").GetProperty("schemas").GetProperty(schema).GetProperty("properties");

        // Assert
        Assert.True(properties.TryGetProperty(property, out _), $"{schema} does not document {property}.");
    }

    [Theory]
    [InlineData("cardNumber")]
    [InlineData("cvv")]
    [InlineData("currency")]
    [InlineData("amount")]
    [InlineData("expiryMonth")]
    [InlineData("expiryYear")]
    public async Task PostPaymentRequest_EachField_HasItsRuleAsDescription(string field)
    {
        // Arrange
        JsonElement document = await GetDocumentAsync();

        // Act
        JsonElement property = document.GetProperty("components").GetProperty("schemas").GetProperty("PostPaymentRequest")
            .GetProperty("properties").GetProperty(field);

        // Assert
        Assert.True(property.TryGetProperty("description", out JsonElement description), $"{field} has no description.");
        Assert.False(string.IsNullOrWhiteSpace(description.GetString()));
    }

    private async Task<JsonElement> GetDocumentAsync()
    {
        using PaymentGatewayFactory factory = new(_bank.Url, SwaggerEnabled);
        using HttpClient client = factory.CreateClient();
        return JsonDocument.Parse(await client.GetStringAsync(DocumentPath)).RootElement.Clone();
    }
}
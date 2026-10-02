using System.Net;

using PaymentGateway.Api.Tests.Integration.Fixtures;

namespace PaymentGateway.Api.Tests.Integration.Http;

public class OpenApiDocumentTests : IClassFixture<WireMockBankFixture>
{
    private const string DocumentPath = "/swagger/v1/swagger.json";

    private readonly WireMockBankFixture _bank;

    public OpenApiDocumentTests(WireMockBankFixture bank)
    {
        _bank = bank;
        _bank.Reset();
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
}
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace PaymentGateway.Api.Tests.EndToEnd;

[Trait("Category", "E2E")]
[Collection(BankSimulatorCollection.Name)]
public class BankFailureJourneyTests
{
    [SimulatorFact]
    public async Task ProcessPayment_WhenBankRefusesTheRequest_Returns502AndDoesNotRetry()
    {
        // Arrange
        using SimulatorGatewayFactory factory = new($"{SimulatorGatewayFactory.SimulatorUrl}/unsupported/");
        using HttpClient client = factory.CreateClient();
        int bankRequestsBefore = await BankSimulator.CountRequestsAsync();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/payments", PaymentJson.Create());

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("bank_error", body.GetProperty("errorCode").GetString());
        Assert.False(body.TryGetProperty("attemptId", out _));
        Assert.Equal(bankRequestsBefore + 1, await BankSimulator.CountRequestsAsync());
    }

    [Fact]
    public async Task ProcessPayment_WhenBankIsUnreachable_Returns503()
    {
        // Arrange
        using SimulatorGatewayFactory factory = new($"http://localhost:{UnusedPort()}");
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/payments", PaymentJson.Create());

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("bank_unavailable", body.GetProperty("errorCode").GetString());
        Assert.False(body.TryGetProperty("attemptId", out _));
    }

    private static int UnusedPort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
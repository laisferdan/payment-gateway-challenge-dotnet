using System.Text.Json;

namespace PaymentGateway.Api.Tests.EndToEnd;

internal static class BankSimulator
{
    private const string ImposterUrl = "http://localhost:2525/imposters/8080";

    private static readonly HttpClient AdminClient = new();

    public static async Task<int> CountRequestsAsync()
    {
        using JsonDocument imposter = JsonDocument.Parse(await AdminClient.GetStringAsync(ImposterUrl));
        return imposter.RootElement.GetProperty("numberOfRequests").GetInt32();
    }
}
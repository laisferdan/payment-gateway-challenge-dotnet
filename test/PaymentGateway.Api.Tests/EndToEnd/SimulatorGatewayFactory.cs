using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.Tests.EndToEnd;

public sealed class SimulatorGatewayFactory : WebApplicationFactory<Program>
{
    public const string SimulatorUrl = "http://localhost:8080";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AcquiringBank:BaseUrl"] = SimulatorUrl,
        }));

        // These tests assert on HTTP responses against the real simulator, not on logs: leaving
        // Program's JSON console logger on just floods `dotnet test` output with every request.
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }
}
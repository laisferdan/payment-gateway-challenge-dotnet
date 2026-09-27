using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PaymentGateway.Api.Tests.EndToEnd;

/// <summary>
/// The gateway in-process against the real bank simulator (<c>docker compose up -d bank_simulator</c>),
/// with the real clock and the real logging – nothing is faked.
/// </summary>
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
    }
}
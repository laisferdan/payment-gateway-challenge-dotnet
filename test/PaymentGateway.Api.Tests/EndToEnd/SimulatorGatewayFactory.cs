using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.Tests.EndToEnd;

public sealed class SimulatorGatewayFactory : WebApplicationFactory<Program>
{
    public const string SimulatorUrl = "http://localhost:8080";

    private readonly string _bankUrl;

    public SimulatorGatewayFactory()
        : this(SimulatorUrl)
    {
    }

    // Internal: xUnit requires a fixture's only public constructor to be the parameterless one.
    internal SimulatorGatewayFactory(string bankUrl)
    {
        _bankUrl = bankUrl;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AcquiringBank:BaseUrl"] = _bankUrl,
        }));

        builder.ConfigureLogging(logging => logging.ClearProviders());
    }
}
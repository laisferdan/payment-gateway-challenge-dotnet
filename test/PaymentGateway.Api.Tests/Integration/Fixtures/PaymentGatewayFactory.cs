using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace PaymentGateway.Api.Tests.Integration.Fixtures;

/// <summary>
/// The gateway in-process with its real pipeline and adapters. Only the acquiring bank is
/// replaced (by a WireMock URL), time is fixed and logs are collected. Runs as Production so no
/// Development-only behaviour leaks into the tests.
/// </summary>
public sealed class PaymentGatewayFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Overrides for tests that exercise the bank timeout: 1 s, the smallest allowed.</summary>
    public static readonly IDictionary<string, string?> ShortBankTimeout = new Dictionary<string, string?>
    {
        ["AcquiringBank:TimeoutSeconds"] = "1",
    };

    private readonly Dictionary<string, string?> _configuration;
    private readonly Action<IServiceCollection>? _configureServices;

    public PaymentGatewayFactory(
        string bankBaseUrl,
        IDictionary<string, string?>? configurationOverrides = null,
        Action<IServiceCollection>? configureServices = null)
    {
        _configuration = new Dictionary<string, string?>
        {
            ["AcquiringBank:BaseUrl"] = bankBaseUrl,
        };
        foreach (KeyValuePair<string, string?> entry in configurationOverrides ?? new Dictionary<string, string?>())
        {
            _configuration[entry.Key] = entry.Value;
        }

        _configureServices = configureServices;
    }

    public FakeTimeProvider TimeProvider { get; } = new(Now);

    public FakeLogCollector LogCollector => Services.GetRequiredService<FakeLogCollector>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(_configuration));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(TimeProvider);
            services.AddLogging(logging => logging.AddFakeLogging());
            _configureServices?.Invoke(services);
        });
    }
}
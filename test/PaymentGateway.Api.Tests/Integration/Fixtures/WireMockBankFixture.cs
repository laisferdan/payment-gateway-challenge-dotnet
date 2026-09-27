using WireMock.Server;

namespace PaymentGateway.Api.Tests.Integration.Fixtures;

/// <summary>
/// Stands in for the acquiring bank at the HTTP boundary. One server per test class; each test
/// resets the stubs so tests never see each other's requests.
/// </summary>
public sealed class WireMockBankFixture : IDisposable
{
    public WireMockBankFixture()
    {
        Server = WireMockServer.Start();
    }

    public WireMockServer Server { get; }

    public string Url => Server.Url!;

    public void Reset()
    {
        Server.Reset();
    }

    public void Dispose()
    {
        Server.Stop();
        Server.Dispose();
    }
}
using WireMock.Server;

namespace PaymentGateway.Api.Tests.Integration.Fixtures;

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
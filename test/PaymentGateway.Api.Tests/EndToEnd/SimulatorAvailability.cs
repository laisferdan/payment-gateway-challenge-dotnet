using System.Net.Sockets;

namespace PaymentGateway.Api.Tests.EndToEnd;

// Checked once per run, so a plain `dotnet test` without Docker skips E2E tests instead of failing them.
internal static class SimulatorAvailability
{
    private const string Host = "localhost";
    private const int Port = 8080;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(500);

    public static readonly Lazy<bool> IsRunning = new(Probe);

    private static bool Probe()
    {
        try
        {
            using TcpClient client = new();
            Task connect = client.ConnectAsync(Host, Port);
            return connect.Wait(ProbeTimeout) && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class SimulatorFactAttribute : FactAttribute
{
    public SimulatorFactAttribute()
    {
        if (!SimulatorAvailability.IsRunning.Value)
        {
            Skip = "The bank simulator is not running (docker compose up -d bank_simulator).";
        }
    }
}

public sealed class SimulatorTheoryAttribute : TheoryAttribute
{
    public SimulatorTheoryAttribute()
    {
        if (!SimulatorAvailability.IsRunning.Value)
        {
            Skip = "The bank simulator is not running (docker compose up -d bank_simulator).";
        }
    }
}
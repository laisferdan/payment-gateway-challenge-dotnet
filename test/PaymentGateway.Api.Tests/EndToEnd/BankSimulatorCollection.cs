namespace PaymentGateway.Api.Tests.EndToEnd;

[CollectionDefinition(Name)]
public sealed class BankSimulatorCollection : ICollectionFixture<SimulatorGatewayFactory>
{
    public const string Name = "Bank simulator";
}
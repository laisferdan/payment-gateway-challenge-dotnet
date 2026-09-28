using System.Runtime.CompilerServices;

namespace PaymentGateway.Api.Tests.Unit.Architecture;

/// <summary>The hexagon's layers, as namespaces of the gateway (Constitution Principle II).</summary>
internal static class Layers
{
    public const string Gateway = "PaymentGateway.Api";
    public const string Domain = Gateway + ".Domain";
    public const string Application = Gateway + ".Application";
    public const string Infrastructure = Gateway + ".Infrastructure";
    public const string Http = Gateway + ".Http";

    public static readonly IReadOnlyList<string> All = [Domain, Application, Infrastructure, Http];

    /// <summary>Every top-level type the gateway's own code declares (not the compiler's).</summary>
    public static IEnumerable<Type> GatewayTypes()
    {
        return typeof(Program).Assembly.GetTypes()
            .Where(type => !type.IsNested
                && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                && type.Namespace is string typeNamespace
                && (typeNamespace == Gateway || typeNamespace.StartsWith(Gateway + ".", StringComparison.Ordinal)));
    }
}
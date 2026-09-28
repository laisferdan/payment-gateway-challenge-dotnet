namespace PaymentGateway.Api.Tests.Unit.Architecture;

/// <summary>
/// Constitution Principle II: inside a layer, every type lives in a folder named by role, never by
/// kind. Namespaces match folders, so checking namespaces checks folders.
/// </summary>
public class FolderNamingTests
{
    private static readonly HashSet<string> KindNames =
    [
        "Services",
        "Repositories",
        "Clients",
        "Controllers",
        "Requests",
        "Responses",
        "Results",
        "Commands",
        "Models",
        "Dtos",
        "Interfaces",
        "Helpers",
        "Utils",
        "Common",
        "Shared",
        "Logs",
    ];

    [Theory]
    [InlineData(Layers.Domain)]
    [InlineData(Layers.Application)]
    [InlineData(Layers.Infrastructure)]
    [InlineData(Layers.Http)]
    public void Layer_AtItsRoot_HoldsNoType(string layer)
    {
        // Arrange

        // Act
        IReadOnlyList<string> typesAtRoot = Layers.GatewayTypes()
            .Where(type => type.Namespace == layer)
            .Select(type => type.FullName!)
            .ToList();

        // Assert
        Assert.Empty(typesAtRoot);
    }

    [Fact]
    public void GatewayType_Always_LivesInALayer()
    {
        // Arrange

        // Act
        IReadOnlyList<string> outsideLayers = Layers.GatewayTypes()
            .Where(type => !Layers.All.Any(layer => type.Namespace!.StartsWith(layer + ".", StringComparison.Ordinal)
                || type.Namespace == layer))
            .Select(type => type.FullName!)
            .ToList();

        // Assert
        Assert.Empty(outsideLayers);
    }

    [Fact]
    public void GatewayNamespaces_Never_NameAKindOfType()
    {
        // Arrange

        // Act
        IReadOnlyList<string> kindNamed = Layers.GatewayTypes()
            .Where(type => type.Namespace!.Split('.').Any(KindNames.Contains))
            .Select(type => type.FullName!)
            .ToList();

        // Assert
        Assert.Empty(kindNamed);
    }

    // Guards the rules above against passing vacuously.
    [Fact]
    public void GatewayTypes_Always_IncludeEveryLayer()
    {
        // Arrange

        // Act
        IReadOnlyList<Type> types = Layers.GatewayTypes().ToList();

        // Assert
        Assert.All(Layers.All, layer => Assert.Contains(types, type => type.Namespace!.StartsWith(layer, StringComparison.Ordinal)));
    }
}
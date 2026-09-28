using System.Net.Http.Json;
using System.Text.Json;

using PaymentGateway.Api.Infrastructure.AcquiringBank;

namespace PaymentGateway.Api.Tests.Unit.Architecture;

/// <summary>
/// Constitution Principle II: the dependency rule points inwards only, and the core stays free of
/// framework and technology types. One project cannot enforce this at compile time, so these tests do.
/// </summary>
public class LayerDependencyTests
{
    private const string Domain = Layers.Domain;
    private const string Application = Layers.Application;
    private const string Infrastructure = Layers.Infrastructure;
    private const string Http = Layers.Http;

    [Theory]
    [InlineData(Domain, Application)]
    [InlineData(Domain, Infrastructure)]
    [InlineData(Domain, Http)]
    [InlineData(Application, Infrastructure)]
    [InlineData(Application, Http)]
    [InlineData(Infrastructure, Http)]
    [InlineData(Http, Infrastructure)]
    public void Layer_OnOuterOrSiblingLayer_HasNoDependency(string layer, string forbiddenLayer)
    {
        // Arrange

        // Act
        IReadOnlyList<string> violations = TypeDependencies.FromLayerOn(layer, forbiddenLayer);

        // Assert
        Assert.Empty(violations);
    }

    [Theory]
    [InlineData(Domain, "Microsoft.AspNetCore")]
    [InlineData(Domain, "System.Net")]
    [InlineData(Domain, "System.Text.Json")]
    [InlineData(Domain, "Microsoft.Extensions")]
    [InlineData(Domain, "System.Data")]
    [InlineData(Application, "Microsoft.AspNetCore")]
    [InlineData(Application, "System.Net")]
    [InlineData(Application, "System.Text.Json")]
    [InlineData(Application, "Microsoft.Extensions.DependencyInjection")]
    [InlineData(Application, "Microsoft.Extensions.Configuration")]
    [InlineData(Application, "Microsoft.Extensions.Options")]
    [InlineData(Application, "System.Data")]
    public void CoreLayer_OnTechnologyNamespace_HasNoDependency(string layer, string technologyNamespace)
    {
        // Arrange

        // Act
        IReadOnlyList<string> violations = TypeDependencies.FromLayerOn(layer, technologyNamespace);

        // Assert
        Assert.Empty(violations);
    }

    // Guards the rules above against passing vacuously: allowed inward dependencies are found.
    [Theory]
    [InlineData(Application, Domain)]
    [InlineData(Infrastructure, Application)]
    [InlineData(Infrastructure, Domain)]
    [InlineData(Http, Application)]
    [InlineData(Http, Domain)]
    public void Layer_OnInnerLayer_HasDependency(string layer, string innerLayer)
    {
        // Arrange

        // Act
        IReadOnlyList<string> dependencies = TypeDependencies.FromLayerOn(layer, innerLayer);

        // Assert
        Assert.NotEmpty(dependencies);
    }

    [Fact]
    public void TypeDependencies_OfAdapter_IncludeTypesUsedOnlyInsideAsyncMethodBodies()
    {
        // Arrange

        // Act
        IReadOnlySet<Type> dependencies = TypeDependencies.Of(typeof(AcquiringBankClient));

        // Assert
        Assert.Contains(typeof(HttpClientJsonExtensions), dependencies);
        Assert.Contains(typeof(JsonException), dependencies);
    }
}
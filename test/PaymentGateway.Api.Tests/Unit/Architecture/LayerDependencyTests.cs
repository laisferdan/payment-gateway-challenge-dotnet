using NetArchTest.Rules;

namespace PaymentGateway.Api.Tests.Unit.Architecture;

// One project cannot enforce the hexagon's dependency rule at compile time, so these tests do.
public class LayerDependencyTests
{
    private const string Domain = "PaymentGateway.Api.Domain";
    private const string Application = "PaymentGateway.Api.Application";
    private const string Infrastructure = "PaymentGateway.Api.Infrastructure";
    private const string Http = "PaymentGateway.Api.Http";

    [Theory]
    [InlineData(Domain, Application)]
    [InlineData(Domain, Infrastructure)]
    [InlineData(Domain, Http)]
    [InlineData(Domain, "Microsoft")]
    [InlineData(Application, Infrastructure)]
    [InlineData(Application, Http)]
    [InlineData(Application, "Microsoft.AspNetCore")]
    [InlineData(Infrastructure, Http)]
    [InlineData(Http, Infrastructure)]
    public void Layer_Never_DependsOnAnOuterOrSiblingLayer(string layer, string forbidden)
    {
        // Arrange
        PredicateList types = Types.InAssembly(typeof(Program).Assembly).That().ResideInNamespace(layer);

        // Act
        TestResult result = types.ShouldNot().HaveDependencyOn(forbidden).GetResult();

        // Assert
        Assert.NotEmpty(types.GetTypes());
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
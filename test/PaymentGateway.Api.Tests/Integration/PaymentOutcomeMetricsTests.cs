using System.Text;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;

using PaymentGateway.Api.Application.ProcessPayment;
using PaymentGateway.Api.Tests.Integration.Fixtures;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace PaymentGateway.Api.Tests.Integration;

// The outcomes counter is recorded once per request: by ProcessPaymentService for everything that
// reaches the use case, and by InvalidModelStateResponder for a framework model-binding failure
// that never reaches it.
public class PaymentOutcomeMetricsTests : IClassFixture<WireMockBankFixture>
{
    private readonly WireMockBankFixture _bank;

    public PaymentOutcomeMetricsTests(WireMockBankFixture bank)
    {
        _bank = bank;
        _bank.Reset();
    }

    [Theory]
    [InlineData(200, """{"authorized":true,"authorization_code":"abc"}""", "authorized")]
    [InlineData(200, """{"authorized":false,"authorization_code":""}""", "declined")]
    [InlineData(503, "", "bank_unavailable")]
    [InlineData(400, "", "bank_error")]
    [InlineData(200, "not json", "bank_outcome_unknown")]
    public async Task Post_WhenBankAnswers_CountsTheOutcomeOnce(int bankStatus, string bankBody, string expectedOutcome)
    {
        // Arrange
        _bank.Server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(bankStatus).WithHeader("Content-Type", "application/json").WithBody(bankBody));
        using PaymentGatewayFactory factory = new(_bank.Url);
        using MetricCollector<long> outcomes = OutcomesCollector(factory);
        using HttpClient client = factory.CreateClient();

        // Act
        await client.PostAsync("/api/payments", new StringContent(ValidPaymentJson, Encoding.UTF8, "application/json"));

        // Assert
        CollectedMeasurement<long> measurement = Assert.Single(outcomes.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal(expectedOutcome, measurement.Tags[PaymentMetrics.OutcomeTag]);
    }

    [Fact]
    public async Task Post_WhenBodyIsUnreadable_CountsARejection()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using MetricCollector<long> outcomes = OutcomesCollector(factory);
        using HttpClient client = factory.CreateClient();

        // Act
        await client.PostAsync("/api/payments", new StringContent("{", Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal("rejected", Assert.Single(outcomes.GetMeasurementSnapshot()).Tags[PaymentMetrics.OutcomeTag]);
    }

    [Fact]
    public async Task Post_WhenFieldsAreInvalid_CountsARejection()
    {
        // Arrange
        using PaymentGatewayFactory factory = new(_bank.Url);
        using MetricCollector<long> outcomes = OutcomesCollector(factory);
        using HttpClient client = factory.CreateClient();

        // Act
        await client.PostAsync("/api/payments", new StringContent("""{"cardNumber":"1234"}""", Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal("rejected", Assert.Single(outcomes.GetMeasurementSnapshot()).Tags[PaymentMetrics.OutcomeTag]);
    }

    private const string ValidPaymentJson =
        """{"cardNumber":"2222405343248877","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":1050,"cvv":"123"}""";

    private static MetricCollector<long> OutcomesCollector(PaymentGatewayFactory factory)
    {
        return new MetricCollector<long>(factory.Meters, PaymentMetrics.MeterName, PaymentMetrics.OutcomesInstrument);
    }
}
using System.Diagnostics.Metrics;

namespace PaymentGateway.Api.Application;

/// <summary>
/// The gateway's instruments (meter <c>PaymentGateway</c>), viewable with <c>dotnet-counters</c>.
/// </summary>
public sealed class PaymentGatewayMetrics
{
    public const string MeterName = "PaymentGateway";

    public const string Authorized = "authorized";
    public const string Declined = "declined";
    public const string Rejected = "rejected";
    public const string BankUnavailable = "bank_unavailable";
    public const string BankError = "bank_error";

    private readonly Counter<long> _paymentOutcomes;
    private readonly Histogram<double> _bankRequestDuration;

    public PaymentGatewayMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);
        _paymentOutcomes = meter.CreateCounter<long>(
            "paymentgateway.payments.outcomes", "{payment}", "Payment requests by outcome.");
        _bankRequestDuration = meter.CreateHistogram<double>(
            "paymentgateway.bank.request.duration", "s", "Duration of acquiring bank calls.");
    }

    public void RecordOutcome(string result)
    {
        _paymentOutcomes.Add(1, new KeyValuePair<string, object?>("result", result));
    }

    public void RecordBankRequestDuration(TimeSpan duration, string outcome)
    {
        _bankRequestDuration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("outcome", outcome));
    }

    public static string ForFailure(BankFailureKind kind)
    {
        return kind == BankFailureKind.Unavailable ? BankUnavailable : BankError;
    }
}
using System.Diagnostics;
using System.Diagnostics.Metrics;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.ProcessPayment;

public sealed class PaymentMetrics
{
    public const string MeterName = "PaymentGateway";
    public const string OutcomesInstrument = "paymentgateway.payments.outcomes";
    public const string OutcomeTag = "result";

    private readonly Counter<long> _outcomes;

    public PaymentMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);
        _outcomes = meter.CreateCounter<long>(OutcomesInstrument, unit: "{payment}", description: "Payment requests by outcome.");
    }

    public void RecordOutcome(ProcessPaymentResult result)
    {
        string tag = result switch
        {
            ProcessPaymentResult.Processed processed => processed.Payment.Status == PaymentStatus.Authorized ? "authorized" : "declined",
            ProcessPaymentResult.Rejected => "rejected",
            ProcessPaymentResult.BankFailed failed => failed.Kind.ToErrorCode(),
            _ => throw new UnreachableException($"Unmapped result {result.GetType().Name}."),
        };
        _outcomes.Add(1, new KeyValuePair<string, object?>(OutcomeTag, tag));
    }
}
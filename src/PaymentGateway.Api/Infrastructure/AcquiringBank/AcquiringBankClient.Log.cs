using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Application.Ports;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

public sealed partial class AcquiringBankClient
{
    [LoggerMessage(EventId = 2000, EventName = "BankCallCompleted", Level = LogLevel.Information,
        Message = "Payment {paymentId} answered {decision} in {elapsedMs} ms")]
    private static partial void LogBankCallCompleted(ILogger logger, Guid paymentId, string decision, long elapsedMs);

    // Error for an unknown outcome – the shopper may have been charged – otherwise Warning. The one log
    // entry for a bank failure, so on-call never has to join it with another to find the payment id.
    [LoggerMessage(EventId = 2001, EventName = "BankCallFailed",
        Message = "Payment {paymentId} (card ending {cardNumberLastFour}) could not be processed: bank {failureKind} " +
            "(HTTP {httpStatusCode}) after {elapsedMs} ms, {currency} {amount}")]
    private static partial void LogBankCallFailed(
        ILogger logger, LogLevel level, Guid paymentId, string cardNumberLastFour, BankFailureKind failureKind,
        int? httpStatusCode, long elapsedMs, string currency, int amount);
}
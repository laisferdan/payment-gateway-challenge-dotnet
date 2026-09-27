using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Application;

namespace PaymentGateway.Api.Infrastructure;

public sealed partial class AcquiringBankClient
{
    [LoggerMessage(EventId = 2000, EventName = "BankCallCompleted", Level = LogLevel.Information,
        Message = "Acquiring bank answered {outcome} in {durationMs} ms")]
    private static partial void LogBankCallCompleted(ILogger logger, long durationMs, string outcome);

    [LoggerMessage(EventId = 2001, EventName = "BankCallFailed", Level = LogLevel.Warning,
        Message = "Acquiring bank call failed ({failureKind}, HTTP {httpStatusCode}) after {durationMs} ms")]
    private static partial void LogBankCallFailed(ILogger logger, long durationMs, BankFailureKind failureKind, int? httpStatusCode);
}

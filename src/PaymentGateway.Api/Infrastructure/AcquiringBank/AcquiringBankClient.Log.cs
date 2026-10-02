using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

public sealed partial class AcquiringBankClient
{
    private void LogOutcome(PaymentRequest request, Guid paymentId, BankCallOutcome outcome, long elapsedMs)
    {
        if (outcome.Result is BankAuthorizationResult.Failed failed)
        {
            LogLevel level = failed.Kind == BankFailureKind.OutcomeUnknown ? LogLevel.Error : LogLevel.Warning;
            LogBankCallFailed(
                _logger, level, outcome.Exception, paymentId, request.CardNumberLastFour, failed.Kind, outcome.FailureReason,
                outcome.HttpStatusCode, elapsedMs, request.Currency, request.Amount);
            return;
        }

        string decision = outcome.Result is BankAuthorizationResult.Authorized
            ? nameof(BankAuthorizationResult.Authorized)
            : nameof(BankAuthorizationResult.Declined);
        LogBankCallCompleted(_logger, paymentId, decision, elapsedMs);
    }

    [LoggerMessage(EventId = 2000, EventName = "BankCallCompleted", Level = LogLevel.Information,
        Message = "Payment {paymentId} answered {decision} in {elapsedMs} ms")]
    private static partial void LogBankCallCompleted(ILogger logger, Guid paymentId, string decision, long elapsedMs);

    [LoggerMessage(EventId = 2001, EventName = "BankCallFailed",
        Message = "Payment {paymentId} (card ending {cardNumberLastFour}) could not be processed: bank {failureKind} " +
            "({failureReason}, HTTP {httpStatusCode}) after {elapsedMs} ms, {currency} {amount}")]
    private static partial void LogBankCallFailed(
        ILogger logger, LogLevel level, Exception? exception, Guid paymentId, string cardNumberLastFour, BankFailureKind failureKind,
        string? failureReason, int? httpStatusCode, long elapsedMs, string currency, int amount);
}
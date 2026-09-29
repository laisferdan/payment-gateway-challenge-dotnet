using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

public sealed partial class AcquiringBankClient
{
    private void LogOutcome(PaymentRequest request, Guid paymentId, BankAuthorizationResult result, int? httpStatusCode, long elapsedMs)
    {
        if (result is BankAuthorizationResult.Failed failed)
        {
            LogLevel level = failed.Kind == BankFailureKind.OutcomeUnknown ? LogLevel.Error : LogLevel.Warning;
            LogBankCallFailed(
                _logger, level, paymentId, request.CardNumberLastFour, failed.Kind, httpStatusCode, elapsedMs,
                request.Currency, request.Amount);
            return;
        }

        string decision = result is BankAuthorizationResult.Authorized
            ? nameof(BankAuthorizationResult.Authorized)
            : nameof(BankAuthorizationResult.Declined);
        LogBankCallCompleted(_logger, paymentId, decision, elapsedMs);
    }

    [LoggerMessage(EventId = 2000, EventName = "BankCallCompleted", Level = LogLevel.Information,
        Message = "Payment {paymentId} answered {decision} in {elapsedMs} ms")]
    private static partial void LogBankCallCompleted(ILogger logger, Guid paymentId, string decision, long elapsedMs);

    [LoggerMessage(EventId = 2001, EventName = "BankCallFailed",
        Message = "Payment {paymentId} (card ending {cardNumberLastFour}) could not be processed: bank {failureKind} " +
            "(HTTP {httpStatusCode}) after {elapsedMs} ms, {currency} {amount}")]
    private static partial void LogBankCallFailed(
        ILogger logger, LogLevel level, Guid paymentId, string cardNumberLastFour, BankFailureKind failureKind,
        int? httpStatusCode, long elapsedMs, string currency, int amount);
}
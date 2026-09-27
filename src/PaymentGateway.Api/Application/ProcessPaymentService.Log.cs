using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

// One entry per payment outcome. None carries a card number, a CVV or a submitted value: a
// rejected request logs field names only, because its values are untrusted and may be card data.
public sealed partial class ProcessPaymentService
{
    [LoggerMessage(EventId = 1000, EventName = "PaymentProcessed", Level = LogLevel.Information,
        Message = "Payment {paymentId} processed: {status}, {currency} {amount}")]
    private static partial void LogPaymentProcessed(ILogger logger, Guid paymentId, PaymentStatus status, string currency, int amount);

    [LoggerMessage(EventId = 1001, EventName = "PaymentRejected", Level = LogLevel.Information,
        Message = "Payment rejected: invalid {invalidFields}")]
    private static partial void LogPaymentRejected(ILogger logger, string invalidFields);

    [LoggerMessage(EventId = 1002, EventName = "PaymentBankFailed", Level = LogLevel.Warning,
        Message = "Payment could not be processed: bank {failureKind}, {currency} {amount}")]
    private static partial void LogPaymentBankFailed(ILogger logger, BankFailureKind failureKind, string currency, int amount);
}
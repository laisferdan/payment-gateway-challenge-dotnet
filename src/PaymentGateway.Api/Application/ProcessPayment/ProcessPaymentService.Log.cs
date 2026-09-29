using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.ProcessPayment;

public sealed partial class ProcessPaymentService
{
    [LoggerMessage(EventId = 1000, EventName = "PaymentProcessed", Level = LogLevel.Information,
        Message = "Payment {paymentId} processed: {status}, {currency} {amount}")]
    private static partial void LogPaymentProcessed(ILogger logger, Guid paymentId, PaymentStatus status, string currency, int amount);

    [LoggerMessage(EventId = 1001, EventName = "PaymentRejected", Level = LogLevel.Information,
        Message = "Payment rejected: invalid {invalidFields}")]
    private static partial void LogPaymentRejected(ILogger logger, string invalidFields);
}
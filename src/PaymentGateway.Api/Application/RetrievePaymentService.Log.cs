using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

public sealed partial class RetrievePaymentService
{
    [LoggerMessage(EventId = 3000, EventName = "PaymentRetrieved", Level = LogLevel.Information,
        Message = "Payment {paymentId} retrieved: {status}")]
    private static partial void LogPaymentRetrieved(ILogger logger, Guid paymentId, PaymentStatus status);

    [LoggerMessage(EventId = 3001, EventName = "PaymentNotFound", Level = LogLevel.Information,
        Message = "Payment {paymentId} not found")]
    private static partial void LogPaymentNotFound(ILogger logger, Guid paymentId);
}
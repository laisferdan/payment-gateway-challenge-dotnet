using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.Http;

// The request body could not be bound to a payment: the raw binding paths are logged, never the
// submitted body content, because the body may hold card data.
public sealed partial class PaymentResultMapper
{
    [LoggerMessage(EventId = 1003, EventName = "PaymentRequestUnreadable", Level = LogLevel.Information,
        Message = "Payment request unreadable: invalid {invalidFields}")]
    private static partial void LogPaymentRequestUnreadable(ILogger logger, string invalidFields);
}
using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.Http;

// The request body could not be bound to a payment: the raw binding paths are logged, never the
// submitted body content, because the body may hold card data.
public sealed partial class PaymentResultMapper
{
    [LoggerMessage(EventId = 1003, EventName = "PaymentRequestUnreadable", Level = LogLevel.Information,
        Message = "Payment request unreadable: invalid {invalidFields}")]
    private static partial void LogPaymentRequestUnreadable(ILogger logger, string invalidFields);

    // The raw submitted value is never a parameter here: it may be anything the merchant pasted, including card data.
    [LoggerMessage(EventId = 3002, EventName = "PaymentIdInvalid", Level = LogLevel.Information,
        Message = "Payment id invalid")]
    private static partial void LogPaymentIdInvalid(ILogger logger);
}
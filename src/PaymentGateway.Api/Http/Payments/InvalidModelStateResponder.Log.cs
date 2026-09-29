using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.Http.Payments;

// Only the binding paths are logged, never the body: it may hold card data.
public sealed partial class InvalidModelStateResponder
{
    [LoggerMessage(EventId = 1003, EventName = "PaymentRequestUnreadable", Level = LogLevel.Information,
        Message = "Payment request unreadable: invalid {invalidFields}")]
    private static partial void LogPaymentRequestUnreadable(ILogger logger, string invalidFields);
}
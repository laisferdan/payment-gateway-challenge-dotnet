using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

using PaymentGateway.Api.Application.ProcessPayment;
using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Http.Payments;

public sealed partial class InvalidModelStateResponder
{
    private const string BodyField = "body";
    private const string JsonPathPrefix = "$.";
    private const string UnreadableBodyMessage = "The request body must be a JSON object with the payment fields.";

    private readonly PaymentResultMapper _mapper;
    private readonly PaymentMetrics _metrics;
    private readonly ILogger<InvalidModelStateResponder> _logger;

    public InvalidModelStateResponder(PaymentResultMapper mapper, PaymentMetrics metrics, ILogger<InvalidModelStateResponder> logger)
    {
        _mapper = mapper;
        _metrics = metrics;
        _logger = logger;
    }

    public IActionResult Respond(ActionContext context)
    {
        List<string> invalidPaths = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .Select(entry => entry.Key)
            .Distinct()
            .ToList();
        LogPaymentRequestUnreadable(_logger, string.Join(",", invalidPaths));
        _metrics.RecordOutcome(new ProcessPaymentResult.Rejected([]));
        return _mapper.PaymentRejected(invalidPaths.Select(ToFieldError).DistinctBy(error => error.Field), context.HttpContext);
    }

    private static (string Field, string Message) ToFieldError(string modelStateKey)
    {
        if (modelStateKey.StartsWith(JsonPathPrefix, StringComparison.Ordinal))
        {
            string jsonName = modelStateKey[JsonPathPrefix.Length..];
            if (PaymentRequest.Messages.ByField.TryGetValue(jsonName, out string? message))
            {
                return (jsonName, message);
            }
        }

        return (BodyField, UnreadableBodyMessage);
    }
}
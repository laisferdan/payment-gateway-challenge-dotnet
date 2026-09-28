using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Http.Payments;

/// <summary>
/// The <c>InvalidModelStateResponseFactory</c>: decides what a request the framework could not bind
/// means for the action it targeted, declared by <see cref="RespondsToInvalidRequestAttribute"/>, and
/// lets <see cref="PaymentResultMapper"/> build the body. Submitted values are never echoed.
/// </summary>
public sealed partial class InvalidModelStateResponder
{
    private const string BodyField = "body";
    private const string JsonPathPrefix = "$.";
    private const string UnreadableBodyMessage = "The request body must be a JSON object with the payment fields.";

    private readonly PaymentResultMapper _mapper;
    private readonly ILogger<InvalidModelStateResponder> _logger;

    public InvalidModelStateResponder(PaymentResultMapper mapper, ILogger<InvalidModelStateResponder> logger)
    {
        _mapper = mapper;
        _logger = logger;
    }

    public IActionResult Respond(ActionContext context)
    {
        InvalidRequestResponse? response = context.ActionDescriptor.EndpointMetadata
            .OfType<RespondsToInvalidRequestAttribute>()
            .FirstOrDefault()?.Response;

        return response switch
        {
            InvalidRequestResponse.PaymentRejected => PaymentRejected(context),
            InvalidRequestResponse.InvalidPaymentId => InvalidPaymentId(context),
            _ => _mapper.ValidationProblem(context.ModelState, context.HttpContext),
        };
    }

    // A value of the wrong type gets its field's rule message; malformed JSON or an empty body gets
    // one "body" error. The binding paths are logged.
    private IActionResult PaymentRejected(ActionContext context)
    {
        List<string> invalidPaths = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .Select(entry => entry.Key)
            .Distinct()
            .ToList();
        LogPaymentRequestUnreadable(_logger, string.Join(",", invalidPaths));
        Dictionary<string, string[]> errors = invalidPaths
            .Select(ToFieldError)
            .DistinctBy(error => error.Field)
            .ToDictionary(error => error.Field, error => new[] { error.Message });
        return _mapper.PaymentRejected(errors, context.HttpContext);
    }

    private IActionResult InvalidPaymentId(ActionContext context)
    {
        LogPaymentIdInvalid(_logger);
        return _mapper.InvalidPaymentId(context.HttpContext);
    }

    // "$.amount" → the amount rule; "$" or "" (malformed JSON, empty body) → the whole body.
    private static (string Field, string Message) ToFieldError(string modelStateKey)
    {
        if (modelStateKey.StartsWith(JsonPathPrefix, StringComparison.Ordinal)
            && PaymentFieldNames.TryParse(modelStateKey[JsonPathPrefix.Length..], out PaymentField field))
        {
            return (PaymentFieldNames.ToJsonName(field), PaymentRequest.Messages.For(field));
        }

        return (BodyField, UnreadableBodyMessage);
    }
}
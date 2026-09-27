using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Application;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Http;

/// <summary>
/// The single place where use-case results become HTTP responses.
/// </summary>
public sealed partial class PaymentResultMapper
{
    private const string ProblemJson = "application/problem+json";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string RejectedTitle = "Payment rejected";
    private const string BankFailureTitle = "Payment could not be processed";
    private const string BadGatewayType = "https://tools.ietf.org/html/rfc9110#section-15.6.3";
    private const string ServiceUnavailableType = "https://tools.ietf.org/html/rfc9110#section-15.6.4";
    private const string InvalidRequestTitle = "Invalid request";
    private const string BodyField = "body";
    private const string JsonPathRoot = "$";
    private const string UnreadableValueMessage = "The value could not be read: check its type and format.";
    private const string UnreadableBodyMessage = "The request body could not be read as a payment request.";

    private readonly ProblemDetailsOptions _problemDetailsOptions;
    private readonly ILogger<PaymentResultMapper> _logger;

    public PaymentResultMapper(IOptions<ProblemDetailsOptions> problemDetailsOptions, ILogger<PaymentResultMapper> logger)
    {
        _problemDetailsOptions = problemDetailsOptions.Value;
        _logger = logger;
    }

    public IActionResult ToActionResult(ProcessPaymentResult result, HttpContext httpContext)
    {
        return result switch
        {
            ProcessPaymentResult.Processed processed => new OkObjectResult(PaymentResponse.From(processed.Payment)),
            ProcessPaymentResult.Rejected rejected => Rejected(ToErrorDictionary(rejected.Errors), httpContext),
            ProcessPaymentResult.BankFailed failed => Problem(BankFailure(failed.Kind), httpContext),
            _ => throw new UnreachableException($"Unmapped result {result.GetType().Name}."),
        };
    }

    /// <summary>
    /// The response for a request whose body or parameters could not be bound. Only the payment
    /// processing action answers with a Rejected payment (<c>paymentStatus</c>); any other action
    /// gets a plain validation problem. Messages are fixed: the submitted values are never echoed.
    /// </summary>
    public IActionResult ToInvalidModelStateResult(ActionContext context)
    {
        IDictionary<string, string[]> errors = ToUnreadableErrors(context.ModelState);
        if (IsProcessPaymentAction(context.ActionDescriptor))
        {
            // The use case never ran, so it could not log this Rejected outcome. Not counted in
            // the PaymentGateway meter: it shows as a 400 in http.server.request.duration.
            LogPaymentRequestUnreadable(_logger, string.Join(",", ToUnreadablePaths(context.ModelState)));
            return Rejected(errors, context.HttpContext);
        }

        ValidationProblemDetails problem = new(errors)
        {
            Type = BadRequestType,
            Title = InvalidRequestTitle,
            Status = StatusCodes.Status400BadRequest,
        };
        return Problem(problem, context.HttpContext);
    }

    // MethodInfo.Name, not ActionName: MVC drops the "Async" suffix from action names.
    private static bool IsProcessPaymentAction(ActionDescriptor descriptor)
    {
        return descriptor is ControllerActionDescriptor { MethodInfo.Name: nameof(PaymentsController.ProcessPaymentAsync) };
    }

    // Binding paths only (e.g. "$.amount"), never the submitted values.
    private static IEnumerable<string> ToUnreadablePaths(ModelStateDictionary modelState)
    {
        List<string> keys = modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .Select(entry => entry.Key.Length == 0 ? JsonPathRoot : entry.Key)
            .ToList();
        return keys.Count > 1 ? keys.Where(key => ToField(key) != BodyField) : keys;
    }

    private static Dictionary<string, string[]> ToUnreadableErrors(ModelStateDictionary modelState)
    {
        List<string> fields = modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .Select(entry => ToField(entry.Key))
            .Distinct()
            .ToList();

        // The framework also reports the whole parameter as missing when a field fails to bind;
        // that entry only adds noise next to the field error.
        if (fields.Count > 1)
        {
            fields.Remove(BodyField);
        }

        return fields.ToDictionary(
            field => field,
            field => new[] { field == BodyField ? UnreadableBodyMessage : UnreadableValueMessage });
    }

    // "$.amount" → "amount"; "$", "" or the parameter name → "body".
    private static string ToField(string modelStateKey)
    {
        const string pathPrefix = JsonPathRoot + ".";
        if (!modelStateKey.StartsWith(pathPrefix, StringComparison.Ordinal))
        {
            return BodyField;
        }

        string path = modelStateKey[pathPrefix.Length..];
        int end = path.IndexOfAny(['.', '[']);
        return end < 0 ? path : path[..end];
    }

    private IActionResult Rejected(IDictionary<string, string[]> errors, HttpContext httpContext)
    {
        PaymentRejectedProblemDetails problem = new(errors)
        {
            Type = BadRequestType,
            Title = RejectedTitle,
            Status = StatusCodes.Status400BadRequest,
        };
        return Problem(problem, httpContext);
    }

    private static BankFailureProblemDetails BankFailure(BankFailureKind kind)
    {
        return kind switch
        {
            BankFailureKind.Unavailable => new BankFailureProblemDetails
            {
                Type = ServiceUnavailableType,
                Title = BankFailureTitle,
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "The acquiring bank is unavailable. Try again later.",
                ErrorCode = BankFailureProblemDetails.BankUnavailable,
            },
            BankFailureKind.Error => new BankFailureProblemDetails
            {
                Type = BadGatewayType,
                Title = BankFailureTitle,
                Status = StatusCodes.Status502BadGateway,
                Detail = "The acquiring bank returned an error.",
                ErrorCode = BankFailureProblemDetails.BankError,
            },
            _ => throw new UnreachableException($"Unmapped bank failure {kind}."),
        };
    }

    // Applies the same customisation (traceId) as every framework-produced ProblemDetails.
    private ObjectResult Problem(ProblemDetails problem, HttpContext httpContext)
    {
        _problemDetailsOptions.CustomizeProblemDetails?.Invoke(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
        ObjectResult result = new(problem) { StatusCode = problem.Status };
        result.ContentTypes.Add(ProblemJson);
        return result;
    }

    private static Dictionary<string, string[]> ToErrorDictionary(IReadOnlyList<ValidationError> errors)
    {
        return errors
            .GroupBy(error => error.Field)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray());
    }

    [LoggerMessage(EventId = 1003, EventName = "PaymentRequestUnreadable", Level = LogLevel.Information,
        Message = "Payment rejected: unreadable {invalidFields}")]
    private static partial void LogPaymentRequestUnreadable(ILogger logger, string invalidFields);
}
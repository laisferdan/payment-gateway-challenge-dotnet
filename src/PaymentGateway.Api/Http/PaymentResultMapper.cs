using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Application;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Http;

public sealed partial class PaymentResultMapper
{
    private const string ProblemJson = "application/problem+json";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string RejectedTitle = "Payment rejected";
    private const string BankFailureTitle = "Payment could not be processed";
    private const string BadGatewayType = "https://tools.ietf.org/html/rfc9110#section-15.6.3";
    private const string ServiceUnavailableType = "https://tools.ietf.org/html/rfc9110#section-15.6.4";
    private const string BodyField = "body";
    private const string JsonPathPrefix = "$.";
    private const string UnreadableBodyMessage = "The request body must be a JSON object with the payment fields.";

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
    /// The <c>InvalidModelStateResponseFactory</c>: a body that could not be read is Rejected like any
    /// other invalid payment. A value of the wrong type gets its field's rule message; malformed JSON
    /// or an empty body gets one <c>body</c> error. The submitted values are never echoed.
    /// </summary>
    public IActionResult ToUnreadableBodyResult(ActionContext context)
    {
        List<string> invalidFields = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .Select(entry => entry.Key)
            .Distinct()
            .ToList();
        LogPaymentRequestUnreadable(_logger, string.Join(",", invalidFields));
        List<ValidationError> errors = invalidFields
            .Select(ToValidationError)
            .DistinctBy(error => error.Field)
            .ToList();
        return ToActionResult(new ProcessPaymentResult.Rejected(errors), context.HttpContext);
    }

    // "$.amount" → the amount rule; "$" or "" (malformed JSON, empty body) → the whole body.
    private static ValidationError ToValidationError(string modelStateKey)
    {
        string? field = modelStateKey.StartsWith(JsonPathPrefix, StringComparison.Ordinal)
            ? PaymentRequest.Fields.All.FirstOrDefault(name =>
                string.Equals(name, modelStateKey[JsonPathPrefix.Length..], StringComparison.OrdinalIgnoreCase))
            : null;
        return field is null
            ? new ValidationError(BodyField, UnreadableBodyMessage)
            : new ValidationError(field, PaymentRequest.Messages.For(field));
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
}
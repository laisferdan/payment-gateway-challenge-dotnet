using System.Diagnostics;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Application.ProcessPayment;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Http.Payments;

// Errors go through the framework's ProblemDetailsFactory so they share its type and traceId.
public sealed class PaymentResultMapper
{
    public const string ProblemJson = "application/problem+json";

    // By route name, not action name: MVC trims the "Async" suffix when generating URLs.
    public const string GetPaymentRouteName = "GetPayment";

    private const string RejectedTitle = "Payment rejected";
    private const string BankFailureTitle = "Payment could not be processed";
    private const string OutcomeUnknownTitle = "Payment could not be confirmed";
    private const string NotFoundTitle = "Payment not found";
    private const string NotFoundDetail = "No payment exists with the given id.";

    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public PaymentResultMapper(ProblemDetailsFactory problemDetailsFactory)
    {
        _problemDetailsFactory = problemDetailsFactory;
    }

    public IActionResult ToProcessResponse(ProcessPaymentResult result, HttpContext httpContext)
    {
        return result switch
        {
            ProcessPaymentResult.Processed processed => new CreatedAtRouteResult(
                GetPaymentRouteName, new { id = processed.Payment.Id }, PaymentResponseDto.From(processed.Payment)),
            ProcessPaymentResult.Rejected rejected => PaymentRejected(
                rejected.Errors.Select(error => (ToJsonName(error.Field), error.Message)), httpContext),
            ProcessPaymentResult.BankFailed failed => BankFailure(failed.PaymentId, failed.Kind, httpContext),
            _ => throw new UnreachableException($"Unmapped result {result.GetType().Name}."),
        };
    }

    public IActionResult ToRetrieveResponse(Payment? payment, HttpContext httpContext)
    {
        return payment is not null
            ? new OkObjectResult(PaymentResponseDto.From(payment))
            : Problem(_problemDetailsFactory.CreateProblemDetails(httpContext, StatusCodes.Status404NotFound, NotFoundTitle, detail: NotFoundDetail));
    }

    // Every source of a Rejected response (domain or model-binding) goes through here.
    public IActionResult PaymentRejected(IEnumerable<(string Field, string Message)> errors, HttpContext httpContext)
    {
        ModelStateDictionary modelState = new();
        foreach ((string field, string message) in errors)
        {
            modelState.AddModelError(field, message);
        }

        ValidationProblemDetails problem = _problemDetailsFactory.CreateValidationProblemDetails(
            httpContext, modelState, StatusCodes.Status400BadRequest, RejectedTitle);
        problem.Extensions["paymentStatus"] = "Rejected";
        return Problem(problem);
    }

    // Only 503 invites a retry: the payment was certainly not made.
    private IActionResult BankFailure(Guid paymentId, BankFailureKind kind, HttpContext httpContext)
    {
        (int status, string title, string detail) = kind switch
        {
            BankFailureKind.Unavailable => (StatusCodes.Status503ServiceUnavailable, BankFailureTitle,
                "The payment was not made: the acquiring bank did not process it. Retrying is safe."),
            BankFailureKind.Error => (StatusCodes.Status502BadGateway, BankFailureTitle,
                "The payment was not made: the acquiring bank refused the request. Retrying will not help."),
            BankFailureKind.OutcomeUnknown => (StatusCodes.Status504GatewayTimeout, OutcomeUnknownTitle,
                "The acquiring bank's answer did not arrive or could not be read, so the payment may have been authorized. Do not retry; quote the traceId to support."),
            _ => throw new UnreachableException($"Unmapped bank failure {kind}."),
        };
        ProblemDetails problem = _problemDetailsFactory.CreateProblemDetails(httpContext, status, title, detail: detail);
        problem.Extensions["errorCode"] = kind.ToErrorCode();
        if (kind == BankFailureKind.OutcomeUnknown)
        {
            // Only here: a 502/503 means the payment was certainly not made, so this id would mislead.
            problem.Extensions["attemptId"] = paymentId;
        }

        return Problem(problem);
    }

    private static ObjectResult Problem(ProblemDetails problem)
    {
        ObjectResult result = new(problem) { StatusCode = problem.Status };
        result.ContentTypes.Add(ProblemJson);
        return result;
    }

    private static string ToJsonName(string pascalCaseField)
    {
        return JsonNamingPolicy.CamelCase.ConvertName(pascalCaseField);
    }
}
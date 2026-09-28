using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Application.ProcessPayment;
using PaymentGateway.Api.Application.RetrievePayment;
using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Http.Payments;

/// <summary>
/// The single place that builds response bodies: it translates use-case results into HTTP and builds
/// every <see cref="ProblemDetails"/> the service returns for them and for unbindable requests.
/// </summary>
public sealed class PaymentResultMapper
{
    public const string ProblemJson = "application/problem+json";
    public const string InvalidIdMessage = "The payment id must be a GUID, e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6.";

    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string RejectedTitle = "Payment rejected";
    private const string ValidationTitle = "One or more validation errors occurred.";
    private const string BankFailureTitle = "Payment could not be processed";
    private const string BadGatewayType = "https://tools.ietf.org/html/rfc9110#section-15.6.3";
    private const string ServiceUnavailableType = "https://tools.ietf.org/html/rfc9110#section-15.6.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string NotFoundTitle = "Payment not found";
    private const string NotFoundDetail = "No payment exists with the given id.";
    private const string InvalidIdTitle = "Invalid payment id";
    private const string InvalidIdField = "id";

    private readonly ProblemDetailsOptions _problemDetailsOptions;

    public PaymentResultMapper(IOptions<ProblemDetailsOptions> problemDetailsOptions)
    {
        _problemDetailsOptions = problemDetailsOptions.Value;
    }

    public IActionResult ToActionResult(ProcessPaymentResult result, HttpContext httpContext)
    {
        return result switch
        {
            ProcessPaymentResult.Processed processed => new OkObjectResult(PaymentResponse.From(processed.Payment)),
            ProcessPaymentResult.Rejected rejected => PaymentRejected(ToErrorDictionary(rejected.Errors), httpContext),
            ProcessPaymentResult.BankFailed failed => Problem(BankFailure(failed.Kind), httpContext),
            _ => throw new UnreachableException($"Unmapped result {result.GetType().Name}."),
        };
    }

    public IActionResult ToActionResult(RetrievePaymentResult result, HttpContext httpContext)
    {
        return result switch
        {
            RetrievePaymentResult.Found found => new OkObjectResult(PaymentResponse.From(found.Payment)),
            RetrievePaymentResult.NotFound => Problem(new ProblemDetails
            {
                Type = NotFoundType,
                Title = NotFoundTitle,
                Detail = NotFoundDetail,
                Status = StatusCodes.Status404NotFound,
            }, httpContext),
            _ => throw new UnreachableException($"Unmapped result {result.GetType().Name}."),
        };
    }

    /// <summary>
    /// A <c>400</c> Rejected payment: <paramref name="errors"/> maps each invalid wire field to its
    /// rule messages, and <c>paymentStatus</c> is <c>Rejected</c>.
    /// </summary>
    public IActionResult PaymentRejected(IDictionary<string, string[]> errors, HttpContext httpContext)
    {
        PaymentRejectedProblemDetails problem = new(errors)
        {
            Type = BadRequestType,
            Title = RejectedTitle,
            Status = StatusCodes.Status400BadRequest,
        };
        return Problem(problem, httpContext);
    }

    /// <summary>A <c>400</c> naming <c>id</c> with a fixed message that never echoes the submitted value.</summary>
    public IActionResult InvalidPaymentId(HttpContext httpContext)
    {
        ValidationProblemDetails problem = new(new Dictionary<string, string[]> { [InvalidIdField] = [InvalidIdMessage] })
        {
            Type = BadRequestType,
            Title = InvalidIdTitle,
            Status = StatusCodes.Status400BadRequest,
        };
        return Problem(problem, httpContext);
    }

    /// <summary>A plain <c>400</c> <see cref="ValidationProblemDetails"/>, with no <c>paymentStatus</c>.</summary>
    public IActionResult ValidationProblem(ModelStateDictionary modelState, HttpContext httpContext)
    {
        ValidationProblemDetails problem = new(modelState)
        {
            Type = BadRequestType,
            Title = ValidationTitle,
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
            .ToDictionary(
                group => PaymentFieldNames.ToJsonName(group.Key),
                group => group.Select(error => error.Message).ToArray());
    }
}
using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Application;

namespace PaymentGateway.Api.Http;

/// <summary>Card payments for merchants.</summary>
[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private const string ProblemJson = "application/problem+json";

    private readonly ProcessPaymentService _processPaymentService;
    private readonly PaymentResultMapper _mapper;

    public PaymentsController(ProcessPaymentService processPaymentService, PaymentResultMapper mapper)
    {
        _processPaymentService = processPaymentService;
        _mapper = mapper;
    }

    /// <summary>Processes a card payment.</summary>
    /// <remarks>
    /// Validates the request and, when valid, sends it once to the acquiring bank. Authorized and
    /// Declined payments are recorded; use the returned <c>id</c> to retrieve them. The full card
    /// number and CVV are never returned – only the last four digits.
    /// </remarks>
    /// <param name="request">The card payment.</param>
    /// <response code="200">The bank decided: <c>status</c> is Authorized or Declined.</response>
    /// <response code="400">
    /// Rejected – invalid information was supplied or the body could not be read. <c>paymentStatus</c>
    /// is <c>Rejected</c> and <c>errors</c> maps each invalid field to its rule; the bank was not called.
    /// </response>
    /// <response code="502">Bank error (<c>errorCode</c> <c>bank_error</c>): retrying will not help. Nothing was recorded.</response>
    /// <response code="503">Bank unavailable (<c>errorCode</c> <c>bank_unavailable</c>): try again later. Nothing was recorded.</response>
    /// <response code="500">Unexpected error. No details are disclosed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentRejectedProblemDetails), StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType(typeof(BankFailureProblemDetails), StatusCodes.Status502BadGateway, ProblemJson)]
    [ProducesResponseType(typeof(BankFailureProblemDetails), StatusCodes.Status503ServiceUnavailable, ProblemJson)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, ProblemJson)]
    public async Task<IActionResult> ProcessPaymentAsync(PostPaymentRequest request)
    {
        ProcessPaymentResult result = await _processPaymentService.ProcessAsync(request.ToCommand());
        return _mapper.ToActionResult(result, HttpContext);
    }
}
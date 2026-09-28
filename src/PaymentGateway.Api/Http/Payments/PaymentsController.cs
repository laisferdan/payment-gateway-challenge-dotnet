using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Application.ProcessPayment;
using PaymentGateway.Api.Application.RetrievePayment;

namespace PaymentGateway.Api.Http.Payments;

/// <summary>Card payments for merchants.</summary>
[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly ProcessPaymentService _processPaymentService;
    private readonly RetrievePaymentService _retrievePaymentService;
    private readonly PaymentResultMapper _mapper;

    public PaymentsController(ProcessPaymentService processPaymentService, RetrievePaymentService retrievePaymentService, PaymentResultMapper mapper)
    {
        _processPaymentService = processPaymentService;
        _retrievePaymentService = retrievePaymentService;
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
    [RespondsToInvalidRequest(InvalidRequestResponse.PaymentRejected)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentRejectedProblemDetails), StatusCodes.Status400BadRequest, PaymentResultMapper.ProblemJson)]
    [ProducesResponseType(typeof(BankFailureProblemDetails), StatusCodes.Status502BadGateway, PaymentResultMapper.ProblemJson)]
    [ProducesResponseType(typeof(BankFailureProblemDetails), StatusCodes.Status503ServiceUnavailable, PaymentResultMapper.ProblemJson)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, PaymentResultMapper.ProblemJson)]
    public async Task<IActionResult> ProcessPaymentAsync(PostPaymentRequest request)
    {
        ProcessPaymentResult result = await _processPaymentService.ProcessAsync(request.ToCommand());
        return _mapper.ToActionResult(result, HttpContext);
    }

    /// <summary>Retrieves a previously processed payment.</summary>
    /// <remarks>
    /// Any GUID notation the platform parses is accepted – canonical, without hyphens, in braces,
    /// in parentheses or the hexadecimal <c>{0x…}</c> form – in any letter case. Surrounding
    /// whitespace is refused, never trimmed.
    /// Retrieval has no side effects and never contacts the acquiring bank.
    /// </remarks>
    /// <param name="id">The payment id returned when the payment was processed.</param>
    /// <response code="200">The payment, with exactly the same fields as the processing response.</response>
    /// <response code="400">
    /// Invalid payment id – the value is not a GUID in any accepted form. No payment was looked
    /// up; unlike the processing <c>400</c>, there is no <c>paymentStatus</c>.
    /// </response>
    /// <response code="404">No Authorized or Declined payment has this id.</response>
    /// <response code="500">Unexpected error. No details are disclosed.</response>
    [HttpGet("{id}")]
    [RespondsToInvalidRequest(InvalidRequestResponse.InvalidPaymentId)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, PaymentResultMapper.ProblemJson)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, PaymentResultMapper.ProblemJson)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, PaymentResultMapper.ProblemJson)]
    public async Task<IActionResult> RetrievePaymentAsync([PaymentIdFromRoute] Guid id)
    {
        RetrievePaymentResult result = await _retrievePaymentService.RetrieveAsync(id);
        return _mapper.ToActionResult(result, HttpContext);
    }
}
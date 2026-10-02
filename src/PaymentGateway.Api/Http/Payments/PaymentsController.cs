using System.Net.Mime;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Application.ProcessPayment;
using PaymentGateway.Api.Application.RetrievePayment;
using PaymentGateway.Api.Domain.Payments;
using PaymentGateway.Api.Http.Payments.Requests;
using PaymentGateway.Api.Http.Payments.Responses;

namespace PaymentGateway.Api.Http.Payments;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly ProcessPaymentService _processPaymentService;
    private readonly RetrievePaymentService _retrievePaymentService;
    private readonly PaymentResultMapper _mapper;

    public PaymentsController(
        ProcessPaymentService processPaymentService,
        RetrievePaymentService retrievePaymentService,
        PaymentResultMapper mapper)
    {
        _processPaymentService = processPaymentService;
        _retrievePaymentService = retrievePaymentService;
        _mapper = mapper;
    }

    /// <summary>Processes a card payment.</summary>
    /// <remarks>
    /// Validates the request and, when valid, sends it once to the acquiring bank. Authorized and
    /// Declined payments are recorded; use the returned <c>id</c> (also in the <c>Location</c>
    /// header) to retrieve them. The full card number and CVV are never returned – only the last
    /// four digits.
    /// </remarks>
    /// <param name="request">The card payment.</param>
    /// <response code="201">The bank decided: <c>status</c> is Authorized or Declined; <c>Location</c> points at the payment.</response>
    /// <response code="400">
    /// Rejected – invalid information was supplied, or the body could not be read. <c>paymentStatus</c>
    /// is <c>Rejected</c> and <c>errors</c> maps each invalid field to its rule; the bank was not called.
    /// </response>
    /// <response code="502">Bank error (<c>errorCode: bank_error</c>): the bank refused the request; retrying will not help. Nothing was recorded.</response>
    /// <response code="503">
    /// Bank unavailable (<c>errorCode: bank_unavailable</c>): the bank could not be reached, or
    /// turned the request away unprocessed (unavailable, timed out or rate-limited), so the payment
    /// was not made; retrying later is safe. Nothing was recorded.
    /// </response>
    /// <response code="504">
    /// Outcome unknown (<c>errorCode: bank_outcome_unknown</c>): the request may have reached the bank but
    /// no usable response came back (timeout, lost connection or an unreadable answer), so the payment may have
    /// been authorized. Do not retry automatically; quote the <c>traceId</c> to support. Nothing was recorded.
    /// </response>
    /// <response code="500">Unexpected error. No details are disclosed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status201Created, MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(PaymentRejectedResponseDto), StatusCodes.Status400BadRequest, PaymentResultMapper.ProblemJson)]
    [ProducesResponseType(typeof(BankFailureResponseDto), StatusCodes.Status502BadGateway, PaymentResultMapper.ProblemJson)]
    [ProducesResponseType(typeof(BankFailureResponseDto), StatusCodes.Status503ServiceUnavailable, PaymentResultMapper.ProblemJson)]
    [ProducesResponseType(typeof(BankFailureResponseDto), StatusCodes.Status504GatewayTimeout, PaymentResultMapper.ProblemJson)]
    [ProducesResponseType(typeof(ProblemResponseDto), StatusCodes.Status500InternalServerError, PaymentResultMapper.ProblemJson)]
    public async Task<IActionResult> ProcessPaymentAsync(ProcessPaymentRequest request)
    {
        ProcessPaymentResult result = await _processPaymentService.ProcessAsync(request.ToCommand());
        return _mapper.ToProcessResponse(result, HttpContext);
    }

    /// <summary>Retrieves a previously processed payment.</summary>
    /// <remarks>
    /// Retrieval has no side effects and never contacts the acquiring bank.
    /// </remarks>
    /// <param name="id">The payment id returned when the payment was processed.</param>
    /// <response code="200">The payment, with exactly the same fields as the processing response.</response>
    /// <response code="404">No Authorized or Declined payment has this id, or the id is not a GUID.</response>
    /// <response code="500">Unexpected error. No details are disclosed.</response>
    [HttpGet("{id:guid}", Name = PaymentResultMapper.GetPaymentRouteName)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ProblemResponseDto), StatusCodes.Status404NotFound, PaymentResultMapper.ProblemJson)]
    [ProducesResponseType(typeof(ProblemResponseDto), StatusCodes.Status500InternalServerError, PaymentResultMapper.ProblemJson)]
    public async Task<IActionResult> RetrievePaymentAsync(Guid id)
    {
        Payment? payment = await _retrievePaymentService.RetrieveAsync(id);
        return _mapper.ToRetrieveResponse(payment, HttpContext);
    }
}
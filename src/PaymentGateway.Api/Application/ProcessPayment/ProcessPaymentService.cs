using System.Diagnostics;

using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.ProcessPayment;

public sealed partial class ProcessPaymentService
{
    private readonly IAcquiringBank _acquiringBank;
    private readonly IPaymentRepository _paymentRepository;
    private readonly PaymentMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProcessPaymentService> _logger;

    public ProcessPaymentService(
        IAcquiringBank acquiringBank,
        IPaymentRepository paymentRepository,
        PaymentMetrics metrics,
        TimeProvider timeProvider,
        ILogger<ProcessPaymentService> logger)
    {
        _acquiringBank = acquiringBank;
        _paymentRepository = paymentRepository;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ProcessPaymentResult> ProcessAsync(
        string? cardNumber, int? expiryMonth, int? expiryYear, string? currency, int? amount, string? cvv)
    {
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        CreatePaymentRequestResult validation = PaymentRequest.Create(
            cardNumber: cardNumber,
            expiryMonth: expiryMonth,
            expiryYear: expiryYear,
            currency: currency,
            amount: amount,
            cvv: cvv,
            today: today);

        ProcessPaymentResult result = validation switch
        {
            CreatePaymentRequestResult.Invalid invalid => Reject(invalid.Errors),
            CreatePaymentRequestResult.Valid valid => await AuthorizeAsync(valid.Request),
            _ => throw new UnreachableException($"Unmapped validation result {validation.GetType().Name}."),
        };

        _metrics.RecordOutcome(result);
        return result;
    }

    private ProcessPaymentResult Reject(IReadOnlyList<ValidationError> errors)
    {
        LogPaymentRejected(_logger, string.Join(",", errors.Select(error => error.Field).Distinct()));
        return new ProcessPaymentResult.Rejected(errors);
    }

    private async Task<ProcessPaymentResult> AuthorizeAsync(PaymentRequest request)
    {
        // Allocated before the bank call so a failure log can still name the attempt.
        Guid paymentId = Guid.NewGuid();
        BankAuthorizationResult decision = await _acquiringBank.RequestAuthorizationAsync(request, paymentId);

        if (decision is BankAuthorizationResult.Failed failure)
        {
            return new ProcessPaymentResult.BankFailed(paymentId, failure.Kind);
        }

        Payment payment = decision is BankAuthorizationResult.Authorized authorized
            ? Payment.Authorized(paymentId, request, authorized.AuthorizationCode)
            : Payment.Declined(paymentId, request);

        // If this throws, the bank has already decided but nothing is stored: see "Unknown outcomes
        // and double charges" in the README for why, and what a production store needs to do instead.
        await _paymentRepository.AddAsync(payment);
        LogPaymentProcessed(_logger, payment.Id, payment.Status, payment.Currency, payment.Amount);
        return new ProcessPaymentResult.Processed(payment);
    }
}
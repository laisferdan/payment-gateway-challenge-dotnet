using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

/// <summary>
/// UC1 – Process a payment: validate, ask the acquiring bank once, record the decision.
/// </summary>
public sealed partial class ProcessPaymentService
{
    private readonly IAcquiringBank _acquiringBank;
    private readonly IPaymentRepository _paymentRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProcessPaymentService> _logger;
    private readonly PaymentGatewayMetrics _metrics;

    public ProcessPaymentService(
        IAcquiringBank acquiringBank,
        IPaymentRepository paymentRepository,
        TimeProvider timeProvider,
        ILogger<ProcessPaymentService> logger,
        PaymentGatewayMetrics metrics)
    {
        _acquiringBank = acquiringBank;
        _paymentRepository = paymentRepository;
        _timeProvider = timeProvider;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<ProcessPaymentResult> ProcessAsync(ProcessPaymentCommand command, CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        CreatePaymentRequestResult validation = PaymentRequest.Create(
            command.CardNumber, command.ExpiryMonth, command.ExpiryYear, command.Currency, command.Amount, command.Cvv, today);
        if (validation.Request is not PaymentRequest request)
        {
            LogPaymentRejected(_logger, string.Join(",", validation.Errors.Select(error => error.Field).Distinct()));
            _metrics.RecordOutcome(PaymentGatewayMetrics.Rejected);
            return new ProcessPaymentResult.Rejected(validation.Errors);
        }

        BankAuthorizationResult decision = await _acquiringBank.RequestAuthorizationAsync(request, cancellationToken);

        if (decision is BankAuthorizationResult.Failed failure)
        {
            LogPaymentBankFailed(_logger, failure.Kind, request.Currency, request.Amount);
            _metrics.RecordOutcome(PaymentGatewayMetrics.ForFailure(failure.Kind));
            return new ProcessPaymentResult.BankFailed(failure.Kind);
        }

        PaymentStatus status = decision is BankAuthorizationResult.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined;
        Payment payment = Payment.Create(request, status);
        _paymentRepository.Add(payment);
        LogPaymentProcessed(_logger, payment.Id, payment.Status, payment.Currency, payment.Amount);
        _metrics.RecordOutcome(status == PaymentStatus.Authorized ? PaymentGatewayMetrics.Authorized : PaymentGatewayMetrics.Declined);
        return new ProcessPaymentResult.Processed(payment);
    }
}
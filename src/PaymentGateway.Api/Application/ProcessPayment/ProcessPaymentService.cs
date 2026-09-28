using System.Diagnostics;

using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Application.Observability;
using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.ProcessPayment;

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

    public async Task<ProcessPaymentResult> ProcessAsync(ProcessPaymentCommand command)
    {
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        CreatePaymentRequestResult validation = PaymentRequest.Create(
            cardNumber: command.CardNumber,
            expiryMonth: command.ExpiryMonth,
            expiryYear: command.ExpiryYear,
            currency: command.Currency,
            amount: command.Amount,
            cvv: command.Cvv,
            today: today);

        return validation switch
        {
            CreatePaymentRequestResult.Invalid invalid => Reject(invalid.Errors),
            CreatePaymentRequestResult.Valid valid => await AuthorizeAsync(valid.Request),
            _ => throw new UnreachableException($"Unmapped validation result {validation.GetType().Name}."),
        };
    }

    private ProcessPaymentResult Reject(IReadOnlyList<ValidationError> errors)
    {
        LogPaymentRejected(_logger, string.Join(",", errors.Select(error => error.Field).Distinct()));
        _metrics.RecordOutcome(PaymentGatewayMetrics.Rejected);
        return new ProcessPaymentResult.Rejected(errors);
    }

    private async Task<ProcessPaymentResult> AuthorizeAsync(PaymentRequest request)
    {
        BankAuthorizationResult decision = await _acquiringBank.RequestAuthorizationAsync(request);

        if (decision is BankAuthorizationResult.Failed failure)
        {
            LogPaymentBankFailed(_logger, failure.Kind, request.Currency, request.Amount);
            _metrics.RecordOutcome(PaymentGatewayMetrics.OutcomeOf(decision));
            return new ProcessPaymentResult.BankFailed(failure.Kind);
        }

        PaymentStatus status = decision is BankAuthorizationResult.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined;
        Payment payment = Payment.Create(request, status);
        await _paymentRepository.AddAsync(payment);
        LogPaymentProcessed(_logger, payment.Id, payment.Status, payment.Currency, payment.Amount);
        _metrics.RecordOutcome(PaymentGatewayMetrics.OutcomeOf(decision));
        return new ProcessPaymentResult.Processed(payment);
    }
}
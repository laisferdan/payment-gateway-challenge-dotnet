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
        Guid paymentId = Guid.NewGuid();
        BankAuthorizationResult decision = await _acquiringBank.RequestAuthorizationAsync(request, paymentId);

        if (decision is BankAuthorizationResult.Failed failure)
        {
            return new ProcessPaymentResult.BankFailed(paymentId, failure.Kind);
        }

        Payment payment = decision is BankAuthorizationResult.Authorized authorized
            ? Payment.Authorized(paymentId, request, authorized.AuthorizationCode)
            : Payment.Declined(paymentId, request);

        try
        {
            await _paymentRepository.AddAsync(payment);
        }
        catch (Exception exception)
        {
            // The bank has decided (an Authorized shopper is charged) but there is no record to retrieve.
            LogPaymentNotRecorded(_logger, exception, payment.Id, payment.Status, payment.AuthorizationCode, payment.Currency, payment.Amount);
            throw;
        }

        LogPaymentProcessed(_logger, payment.Id, payment.Status, payment.Currency, payment.Amount);
        return new ProcessPaymentResult.Processed(payment);
    }
}
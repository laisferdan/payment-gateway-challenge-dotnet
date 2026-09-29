using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.RetrievePayment;

public sealed partial class RetrievePaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ILogger<RetrievePaymentService> _logger;

    public RetrievePaymentService(IPaymentRepository paymentRepository, ILogger<RetrievePaymentService> logger)
    {
        _paymentRepository = paymentRepository;
        _logger = logger;
    }

    public async Task<Payment?> RetrieveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Payment? payment = await _paymentRepository.GetByIdAsync(id, cancellationToken);
        if (payment is null)
        {
            LogPaymentNotFound(_logger, id);
            return null;
        }

        LogPaymentRetrieved(_logger, payment.Id, payment.Status);
        return payment;
    }
}
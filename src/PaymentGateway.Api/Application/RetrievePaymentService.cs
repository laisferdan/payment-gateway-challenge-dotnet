using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

public sealed partial class RetrievePaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ILogger<RetrievePaymentService> _logger;

    public RetrievePaymentService(IPaymentRepository paymentRepository, ILogger<RetrievePaymentService> logger)
    {
        _paymentRepository = paymentRepository;
        _logger = logger;
    }

    public RetrievePaymentResult Retrieve(Guid id)
    {
        Payment? payment = _paymentRepository.GetById(id);
        if (payment is null)
        {
            LogPaymentNotFound(_logger, id);
            return new RetrievePaymentResult.NotFound();
        }

        LogPaymentRetrieved(_logger, payment.Id, payment.Status);
        return new RetrievePaymentResult.Found(payment);
    }
}
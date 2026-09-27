using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application;

public interface IPaymentRepository
{
    void Add(Payment payment);
}
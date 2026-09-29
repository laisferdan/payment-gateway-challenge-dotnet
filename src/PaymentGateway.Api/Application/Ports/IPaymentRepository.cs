using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Ports;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment);

    Task<Payment?> GetByIdAsync(Guid id);
}
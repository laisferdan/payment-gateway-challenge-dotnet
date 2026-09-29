using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Ports;

/// <summary>Driven port that stores processed payments.</summary>
public interface IPaymentRepository
{
    Task AddAsync(Payment payment);

    /// <summary>Returns <c>null</c> when no payment has this id.</summary>
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
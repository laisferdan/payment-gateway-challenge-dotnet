using System.Collections.Concurrent;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Infrastructure.Persistence;

/// <summary>
/// Payments kept in memory, as the assessment allows: safe for concurrent requests, lost on restart.
/// </summary>
public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public Task AddAsync(Payment payment)
    {
        _payments[payment.Id] = payment;
        return Task.CompletedTask;
    }

    public Task<Payment?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(_payments.TryGetValue(id, out Payment? payment) ? payment : null);
    }
}
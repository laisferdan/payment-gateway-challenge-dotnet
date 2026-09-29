using System.Collections.Concurrent;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Infrastructure.Persistence;

public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public Task AddAsync(Payment payment)
    {
        if (!_payments.TryAdd(payment.Id, payment))
        {
            throw new InvalidOperationException($"A payment with id {payment.Id} already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<Payment?>(cancellationToken);
        }

        return Task.FromResult(_payments.TryGetValue(id, out Payment? payment) ? payment : null);
    }
}
using System.Collections.Concurrent;

using PaymentGateway.Api.Application;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Infrastructure;

/// <summary>
/// Payments kept in memory, as the assessment allows: safe for concurrent requests, lost on restart.
/// </summary>
public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public void Add(Payment payment)
    {
        _payments[payment.Id] = payment;
    }
}
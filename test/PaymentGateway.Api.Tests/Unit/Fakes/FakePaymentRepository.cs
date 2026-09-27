using PaymentGateway.Api.Application;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Tests.Unit.Fakes;

/// <summary>Records every payment added to it.</summary>
public sealed class FakePaymentRepository : IPaymentRepository
{
    private readonly List<Payment> _payments = [];

    public IReadOnlyList<Payment> Payments => _payments;

    public void Add(Payment payment)
    {
        _payments.Add(payment);
    }
}
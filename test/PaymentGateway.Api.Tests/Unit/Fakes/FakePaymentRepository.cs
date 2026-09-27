using PaymentGateway.Api.Application;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Tests.Unit.Fakes;

public sealed class FakePaymentRepository : IPaymentRepository
{
    private readonly List<Payment> _payments = [];

    public IReadOnlyList<Payment> Payments => _payments;

    public void Add(Payment payment)
    {
        _payments.Add(payment);
    }

    public Payment? GetById(Guid id)
    {
        return _payments.FirstOrDefault(payment => payment.Id == id);
    }
}
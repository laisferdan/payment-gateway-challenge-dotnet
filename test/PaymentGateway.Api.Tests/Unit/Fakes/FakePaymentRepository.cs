using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Tests.Unit.Fakes;

public sealed class FakePaymentRepository : IPaymentRepository
{
    private readonly List<Payment> _payments = [];

    public IReadOnlyList<Payment> Payments => _payments;

    public Exception? AddFailure { get; init; }

    public Task AddAsync(Payment payment)
    {
        if (AddFailure is not null)
        {
            throw AddFailure;
        }

        _payments.Add(payment);
        return Task.CompletedTask;
    }

    public Task<Payment?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(_payments.FirstOrDefault(payment => payment.Id == id));
    }
}
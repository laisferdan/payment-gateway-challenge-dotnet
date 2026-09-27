using PaymentGateway.Api.Application;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Tests.Unit.Fakes;

/// <summary>Answers with a configured result and records how it was called.</summary>
public sealed class FakeAcquiringBank : IAcquiringBank
{
    private readonly BankAuthorizationResult _result;

    public FakeAcquiringBank(BankAuthorizationResult result)
    {
        _result = result;
    }

    public int CallCount { get; private set; }

    public PaymentRequest? LastRequest { get; private set; }

    public Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        return Task.FromResult(_result);
    }
}
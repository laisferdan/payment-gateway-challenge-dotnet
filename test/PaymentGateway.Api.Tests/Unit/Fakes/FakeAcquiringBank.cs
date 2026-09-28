using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Tests.Unit.Fakes;

public sealed class FakeAcquiringBank : IAcquiringBank
{
    private readonly BankAuthorizationResult _result;

    public FakeAcquiringBank(BankAuthorizationResult result)
    {
        _result = result;
    }

    public int CallCount { get; private set; }

    public PaymentRequest? LastRequest { get; private set; }

    public Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest request)
    {
        CallCount++;
        LastRequest = request;
        return Task.FromResult(_result);
    }
}
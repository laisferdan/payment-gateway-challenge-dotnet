using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Application.Observability;
using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

/// <summary>
/// The acquiring bank (simulator) over HTTP: <c>POST {BaseUrl}/payments</c>. Makes exactly one call
/// per payment – no retries, a payment request is not idempotent – and classifies every answer.
/// </summary>
public sealed partial class AcquiringBankClient : IAcquiringBank
{
    private const string PaymentsPath = "payments";

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly PaymentGatewayMetrics _metrics;
    private readonly ILogger<AcquiringBankClient> _logger;

    public AcquiringBankClient(
        HttpClient httpClient,
        TimeProvider timeProvider,
        PaymentGatewayMetrics metrics,
        ILogger<AcquiringBankClient> logger)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest request)
    {
        long start = _timeProvider.GetTimestamp();
        int? httpStatusCode = null;
        BankAuthorizationResult result;
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                PaymentsPath, BankPaymentRequest.From(request));
            httpStatusCode = (int)response.StatusCode;
            result = await ClassifyAsync(response);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            result = new BankAuthorizationResult.Failed(BankFailureKind.Unavailable);
        }

        Record(result, _timeProvider.GetElapsedTime(start), httpStatusCode);
        return result;
    }

    private void Record(BankAuthorizationResult result, TimeSpan duration, int? httpStatusCode)
    {
        string outcome = PaymentGatewayMetrics.OutcomeOf(result);
        _metrics.RecordBankRequestDuration(duration, outcome);

        if (result is BankAuthorizationResult.Failed failed)
        {
            LogBankCallFailed(_logger, (long)duration.TotalMilliseconds, failed.Kind, httpStatusCode);
            return;
        }

        LogBankCallCompleted(_logger, (long)duration.TotalMilliseconds, outcome);
    }

    private static async Task<BankAuthorizationResult> ClassifyAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return new BankAuthorizationResult.Failed(BankFailureKind.Unavailable);
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return new BankAuthorizationResult.Failed(BankFailureKind.Error);
        }

        BankPaymentResponse? body = await ReadBodyAsync(response);
        return body switch
        {
            { Authorized: true, AuthorizationCode: { Length: > 0 } } => new BankAuthorizationResult.Authorized(),
            { Authorized: false } => new BankAuthorizationResult.Declined(),
            _ => new BankAuthorizationResult.Failed(BankFailureKind.Error),
        };
    }

    private static async Task<BankPaymentResponse?> ReadBodyAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<BankPaymentResponse>();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
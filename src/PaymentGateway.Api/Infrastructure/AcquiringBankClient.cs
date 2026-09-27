using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Application;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Infrastructure;

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

    public async Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        long start = _timeProvider.GetTimestamp();
        int? httpStatusCode = null;
        BankAuthorizationResult result;
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                PaymentsPath, BankPaymentRequest.From(request), cancellationToken);
            httpStatusCode = (int)response.StatusCode;
            result = await ClassifyAsync(response, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout surfaces as a cancellation the caller did not ask for.
            result = new BankAuthorizationResult.Failed(BankFailureKind.Unavailable);
        }
        catch (HttpRequestException)
        {
            result = new BankAuthorizationResult.Failed(BankFailureKind.Unavailable);
        }

        Record(result, _timeProvider.GetElapsedTime(start), httpStatusCode);
        return result;
    }

    private void Record(BankAuthorizationResult result, TimeSpan duration, int? httpStatusCode)
    {
        if (result is BankAuthorizationResult.Failed failed)
        {
            _metrics.RecordBankRequestDuration(duration, PaymentGatewayMetrics.ForFailure(failed.Kind));
            LogBankCallFailed(_logger, (long)duration.TotalMilliseconds, failed.Kind, httpStatusCode);
            return;
        }

        string outcome = result is BankAuthorizationResult.Authorized ? PaymentGatewayMetrics.Authorized : PaymentGatewayMetrics.Declined;
        _metrics.RecordBankRequestDuration(duration, outcome);
        LogBankCallCompleted(_logger, (long)duration.TotalMilliseconds, outcome);
    }

    private static async Task<BankAuthorizationResult> ClassifyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return new BankAuthorizationResult.Failed(BankFailureKind.Unavailable);
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return new BankAuthorizationResult.Failed(BankFailureKind.Error);
        }

        BankPaymentResponse? body = await ReadBodyAsync(response, cancellationToken);
        return body switch
        {
            { Authorized: true, AuthorizationCode: { Length: > 0 } } => new BankAuthorizationResult.Authorized(),
            { Authorized: false } => new BankAuthorizationResult.Declined(),
            // Missing "authorized", or authorized without a code: the answer cannot be trusted.
            _ => new BankAuthorizationResult.Failed(BankFailureKind.Error),
        };
    }

    private static async Task<BankPaymentResponse?> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(EventId = 2000, EventName = "BankCallCompleted", Level = LogLevel.Information,
        Message = "Acquiring bank answered {outcome} in {durationMs} ms")]
    private static partial void LogBankCallCompleted(ILogger logger, long durationMs, string outcome);

    [LoggerMessage(EventId = 2001, EventName = "BankCallFailed", Level = LogLevel.Warning,
        Message = "Acquiring bank call failed ({failureKind}, HTTP {httpStatusCode}) after {durationMs} ms")]
    private static partial void LogBankCallFailed(ILogger logger, long durationMs, BankFailureKind failureKind, int? httpStatusCode);
}
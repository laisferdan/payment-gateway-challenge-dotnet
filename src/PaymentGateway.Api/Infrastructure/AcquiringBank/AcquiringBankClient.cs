using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

// One call per payment, never retried: a retry could charge the shopper twice.
public sealed partial class AcquiringBankClient : IAcquiringBank
{
    private const string PaymentsPath = "payments";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AcquiringBankClient> _logger;

    public AcquiringBankClient(HttpClient httpClient, ILogger<AcquiringBankClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest request, Guid paymentId)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                PaymentsPath, BankPaymentRequest.From(request));
            BankAuthorizationResult result = await ClassifyAsync(response);
            if (result is BankAuthorizationResult.Failed failed)
            {
                LogFailed(request, paymentId, failed.Kind, (int)response.StatusCode, started);
            }
            else
            {
                LogBankCallCompleted(_logger, paymentId, result is BankAuthorizationResult.Authorized ? "Authorized" : "Declined", ElapsedMs(started));
            }

            return result;
        }
        catch (HttpRequestException exception) when (NeverReachedTheBank(exception))
        {
            return Failed(request, paymentId, BankFailureKind.Unavailable, started);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // Timed out or lost after the request was sent: the bank may have authorized it.
            return Failed(request, paymentId, BankFailureKind.OutcomeUnknown, started);
        }
    }

    private static long ElapsedMs(long started)
    {
        return (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private BankAuthorizationResult.Failed Failed(PaymentRequest request, Guid paymentId, BankFailureKind kind, long started)
    {
        LogFailed(request, paymentId, kind, httpStatusCode: null, started);
        return new BankAuthorizationResult.Failed(kind);
    }

    private void LogFailed(PaymentRequest request, Guid paymentId, BankFailureKind kind, int? httpStatusCode, long started)
    {
        LogBankCallFailed(
            _logger, LevelFor(kind), paymentId, request.CardNumberLastFour, kind, httpStatusCode, ElapsedMs(started),
            request.Currency, request.Amount);
    }

    private static LogLevel LevelFor(BankFailureKind kind)
    {
        return kind == BankFailureKind.OutcomeUnknown ? LogLevel.Error : LogLevel.Warning;
    }

    // The connection was never established, so no payment request was sent.
    private static bool NeverReachedTheBank(HttpRequestException exception)
    {
        return exception.HttpRequestError is HttpRequestError.ConnectionError
            or HttpRequestError.NameResolutionError
            or HttpRequestError.SecureConnectionError;
    }

    private static async Task<BankAuthorizationResult> ClassifyAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return new BankAuthorizationResult.Failed(BankFailureKind.Unavailable);
        }

        // Any other 5xx (the bank's own 500s, or a proxy's 502/504 in front of it) can happen after an
        // authorization was already committed, so – like an unreadable 200 – the outcome is unknown,
        // not an error: only a 4xx means the bank itself refused the request outright.
        if ((int)response.StatusCode >= 500)
        {
            return new BankAuthorizationResult.Failed(BankFailureKind.OutcomeUnknown);
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return new BankAuthorizationResult.Failed(BankFailureKind.Error);
        }

        // An unreadable 200 may hide an authorization: the outcome is unknown, not an error.
        BankPaymentResponse? body = await ReadBodyAsync(response);
        return body switch
        {
            { Authorized: true, AuthorizationCode: { Length: > 0 } code } => new BankAuthorizationResult.Authorized(code),
            { Authorized: false } => new BankAuthorizationResult.Declined(),
            _ => new BankAuthorizationResult.Failed(BankFailureKind.OutcomeUnknown),
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
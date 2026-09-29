using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

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
        (BankAuthorizationResult result, int? httpStatusCode) = await PostPaymentAsync(request);
        LogOutcome(request, paymentId, result, httpStatusCode, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return result;
    }

    private async Task<(BankAuthorizationResult Result, int? HttpStatusCode)> PostPaymentAsync(PaymentRequest request)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(PaymentsPath, BankPaymentRequest.From(request));
            return (await ClassifyResponseAsync(response), (int)response.StatusCode);
        }
        catch (HttpRequestException exception) when (NeverReachedTheBank(exception))
        {
            return (new BankAuthorizationResult.Failed(BankFailureKind.Unavailable), null);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return (new BankAuthorizationResult.Failed(BankFailureKind.OutcomeUnknown), null);
        }
    }

    private static bool NeverReachedTheBank(HttpRequestException exception)
    {
        return exception.HttpRequestError is HttpRequestError.ConnectionError
            or HttpRequestError.NameResolutionError
            or HttpRequestError.SecureConnectionError;
    }

    private static async Task<BankAuthorizationResult> ClassifyResponseAsync(HttpResponseMessage response)
    {
        return response.StatusCode switch
        {
            HttpStatusCode.OK => ClassifyBody(await ReadBodyOrNullAsync(response)),
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                => new BankAuthorizationResult.Failed(BankFailureKind.Unavailable),
            >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError => new BankAuthorizationResult.Failed(BankFailureKind.Error),
            _ => new BankAuthorizationResult.Failed(BankFailureKind.OutcomeUnknown),
        };
    }

    private static async Task<BankPaymentResponse?> ReadBodyOrNullAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<BankPaymentResponse>();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static BankAuthorizationResult ClassifyBody(BankPaymentResponse? body)
    {
        return body switch
        {
            { Authorized: true, AuthorizationCode: { Length: > 0 } code } => new BankAuthorizationResult.Authorized(code),
            { Authorized: false } => new BankAuthorizationResult.Declined(),
            _ => new BankAuthorizationResult.Failed(BankFailureKind.OutcomeUnknown),
        };
    }
}
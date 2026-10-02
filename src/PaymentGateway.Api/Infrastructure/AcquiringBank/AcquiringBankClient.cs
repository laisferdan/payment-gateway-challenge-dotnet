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
        BankCallOutcome outcome = await PostPaymentAsync(request);
        LogOutcome(request, paymentId, outcome, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return outcome.Result;
    }

    private async Task<BankCallOutcome> PostPaymentAsync(PaymentRequest request)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(PaymentsPath, BankPaymentRequest.From(request));
            return await ClassifyResponseAsync(response);
        }
        catch (HttpRequestException exception) when (NeverReachedTheBank(exception))
        {
            return BankCallOutcome.Failed(BankFailureKind.Unavailable, exception.HttpRequestError.ToString(), exception: exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return BankCallOutcome.Failed(BankFailureKind.OutcomeUnknown, DescribeTransportFailure(exception), exception: exception);
        }
    }

    private static bool NeverReachedTheBank(HttpRequestException exception)
    {
        return exception.HttpRequestError is HttpRequestError.ConnectionError
            or HttpRequestError.NameResolutionError
            or HttpRequestError.SecureConnectionError;
    }

    private static string DescribeTransportFailure(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpRequestException => httpRequestException.HttpRequestError.ToString(),
            OperationCanceledException { InnerException: TimeoutException } => "Timeout",
            _ => "Canceled",
        };
    }

    private static async Task<BankCallOutcome> ClassifyResponseAsync(HttpResponseMessage response)
    {
        int httpStatusCode = (int)response.StatusCode;
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await ClassifyBodyAsync(response, httpStatusCode),
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                => BankCallOutcome.Failed(BankFailureKind.Unavailable, "UnexpectedStatus", httpStatusCode),
            >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError
                => BankCallOutcome.Failed(BankFailureKind.Error, "UnexpectedStatus", httpStatusCode),
            _ => BankCallOutcome.Failed(BankFailureKind.OutcomeUnknown, "UnexpectedStatus", httpStatusCode),
        };
    }

    private static async Task<BankCallOutcome> ClassifyBodyAsync(HttpResponseMessage response, int httpStatusCode)
    {
        BankPaymentResponse? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<BankPaymentResponse>();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return BankCallOutcome.Failed(BankFailureKind.OutcomeUnknown, "UnreadableBody", httpStatusCode, exception);
        }

        BankAuthorizationResult result = ClassifyBody(body);
        return result is BankAuthorizationResult.Failed
            ? new BankCallOutcome(result, httpStatusCode, "IncompleteBody")
            : new BankCallOutcome(result, httpStatusCode);
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
using Microsoft.AspNetCore.Mvc;

namespace PaymentGateway.Api.Http;

/// <summary>
/// The body of a <c>503</c> or <c>502</c> of <c>POST /api/payments</c>: the acquiring bank gave no
/// decision, so nothing was recorded. It is not a payment status – there is no <c>paymentStatus</c>.
/// </summary>
public sealed class BankFailureProblemDetails : ProblemDetails
{
    /// <summary>The bank is unavailable or did not answer in time; trying again later may succeed.</summary>
    public const string BankUnavailable = "bank_unavailable";

    /// <summary>The bank answered with an error or with a response that cannot be trusted; retrying will not help.</summary>
    public const string BankError = "bank_error";

    /// <summary><c>bank_unavailable</c> or <c>bank_error</c>.</summary>
    public required string ErrorCode { get; init; }
}
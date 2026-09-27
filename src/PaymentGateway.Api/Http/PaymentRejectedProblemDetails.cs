using Microsoft.AspNetCore.Mvc;

namespace PaymentGateway.Api.Http;

/// <summary>
/// The body of every <c>400</c> of <c>POST /api/payments</c>: the request was Rejected, the bank was
/// not called and no payment was recorded. <c>errors</c> lists every invalid field.
/// </summary>
public sealed class PaymentRejectedProblemDetails : ValidationProblemDetails
{
    public const string RejectedStatus = "Rejected";

    public PaymentRejectedProblemDetails(IDictionary<string, string[]> errors)
        : base(errors)
    {
    }

    /// <summary>Always <c>Rejected</c> – the assessment's third payment outcome.</summary>
    public string PaymentStatus { get; } = RejectedStatus;
}
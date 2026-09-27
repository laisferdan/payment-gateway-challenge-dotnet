using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Http;

/// <summary>
/// The body of every <c>400</c> of <c>POST /api/payments</c>: the request was Rejected, the bank was
/// not called and no payment was recorded. <c>errors</c> lists every invalid field.
/// </summary>
public sealed class PaymentRejectedProblemDetails : ValidationProblemDetails
{
    public PaymentRejectedProblemDetails(IDictionary<string, string[]> errors)
        : base(errors)
    {
    }

    /// <summary>Always <c>Rejected</c>.</summary>
    public PaymentStatus PaymentStatus { get; } = PaymentStatus.Rejected;
}
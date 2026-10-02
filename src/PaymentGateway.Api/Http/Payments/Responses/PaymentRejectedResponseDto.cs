namespace PaymentGateway.Api.Http.Payments.Responses;

// Documents the ValidationProblemDetails body PaymentResultMapper.PaymentRejected writes; never serialized itself.

/// <summary>A Rejected payment (RFC 7807, <c>application/problem+json</c>): every invalid field with its rule. The bank was not called.</summary>
public sealed class PaymentRejectedResponseDto
{
    /// <summary>Always <c>Payment rejected</c>.</summary>
    /// <example>Payment rejected</example>
    public required string Title { get; init; }

    /// <summary>Always 400.</summary>
    /// <example>400</example>
    public required int Status { get; init; }

    /// <summary>Each invalid field, or <c>body</c> when the body could not be read, with the rule it broke.</summary>
    /// <example>{"cardNumber":["Card number must be a string of 14 to 19 digits (0-9)."],"currency":["Currency must be one of: GBP, EUR, USD."]}</example>
    public required PaymentRejectedErrorsDto Errors { get; init; }

    /// <summary>W3C trace id of the request (also in the <c>X-Trace-Id</c> header and the gateway's logs).</summary>
    /// <example>4bf92f3577b34da6a3ce929d0e0e4736</example>
    public required string TraceId { get; init; }

    /// <summary>Always <c>Rejected</c>.</summary>
    /// <example>Rejected</example>
    public required string PaymentStatus { get; init; }
}
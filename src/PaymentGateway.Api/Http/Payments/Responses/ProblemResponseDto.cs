namespace PaymentGateway.Api.Http.Payments.Responses;

// Documents the ProblemDetails body PaymentResultMapper and the framework write; never serialized itself.

/// <summary>An error (RFC 7807, <c>application/problem+json</c>). No internal details are disclosed.</summary>
public sealed class ProblemResponseDto
{
    /// <summary>A short, human-readable summary of the problem.</summary>
    /// <example>Payment not found</example>
    public required string Title { get; init; }

    /// <summary>The HTTP status code.</summary>
    /// <example>404</example>
    public required int Status { get; init; }

    /// <summary>An explanation of this occurrence, when there is one.</summary>
    /// <example>No payment exists with the given id.</example>
    public string? Detail { get; init; }

    /// <summary>W3C trace id of the request (also in the <c>X-Trace-Id</c> header and the gateway's logs); quote it to support.</summary>
    /// <example>4bf92f3577b34da6a3ce929d0e0e4736</example>
    public required string TraceId { get; init; }
}
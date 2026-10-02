namespace PaymentGateway.Api.Http.Payments.Responses;

// Documents the ProblemDetails body PaymentResultMapper writes for a bank failure; never serialized itself.

/// <summary>The acquiring bank did not decide the payment (RFC 7807, <c>application/problem+json</c>). Nothing was recorded.</summary>
public sealed class BankFailureResponseDto
{
    /// <summary><c>Payment could not be processed</c>, or <c>Payment could not be confirmed</c> when the outcome is unknown.</summary>
    /// <example>Payment could not be processed</example>
    public required string Title { get; init; }

    /// <summary>502, 503 or 504.</summary>
    /// <example>503</example>
    public required int Status { get; init; }

    /// <summary>What happened to the payment and whether retrying is safe.</summary>
    /// <example>The payment was not made: the acquiring bank did not process it. Retrying is safe.</example>
    public required string Detail { get; init; }

    /// <summary>W3C trace id of the request (also in the <c>X-Trace-Id</c> header and the gateway's logs); quote it to support.</summary>
    /// <example>4bf92f3577b34da6a3ce929d0e0e4736</example>
    public required string TraceId { get; init; }

    /// <summary><c>bank_error</c> (502), <c>bank_unavailable</c> (503) or <c>bank_outcome_unknown</c> (504).</summary>
    /// <example>bank_unavailable</example>
    public required string ErrorCode { get; init; }

    /// <summary>Only with <c>bank_outcome_unknown</c>: the id the attempt was logged under, for reconciliation with the bank.</summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    public Guid? AttemptId { get; init; }
}
namespace PaymentGateway.Api.Domain.PaymentRequests;

public sealed record ValidationError(string Field, string Message);
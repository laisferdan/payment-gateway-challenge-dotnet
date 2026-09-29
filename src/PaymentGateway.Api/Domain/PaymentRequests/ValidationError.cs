namespace PaymentGateway.Api.Domain.PaymentRequests;

// Field is the C# property name; the HTTP adapter turns it into the wire name.
public sealed record ValidationError(string Field, string Message);
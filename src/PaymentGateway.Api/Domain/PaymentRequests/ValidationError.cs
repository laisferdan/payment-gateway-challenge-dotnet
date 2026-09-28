namespace PaymentGateway.Api.Domain.PaymentRequests;

/// <summary>
/// One broken rule: the request field and a message naming the rule. The message never contains
/// the submitted value, which may be card data.
/// </summary>
public sealed record ValidationError(PaymentField Field, string Message);
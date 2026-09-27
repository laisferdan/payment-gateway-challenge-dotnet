namespace PaymentGateway.Api.Domain;

/// <summary>
/// One broken rule: the request field (camelCase, as the merchant sent it) and a message naming the
/// rule. The message never contains the submitted value, which may be card data.
/// </summary>
public sealed record ValidationError(string Field, string Message);
using Microsoft.OpenApi.Models;

using PaymentGateway.Api.Domain.PaymentRequests;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PaymentGateway.Api.Http.Payments;

/// <summary>
/// Documents each <see cref="PostPaymentRequest"/> field with the domain's own rule message, so the
/// OpenAPI document and a Rejected response always say the same thing.
/// </summary>
public sealed class PaymentRuleSchemaFilter : ISchemaFilter
{
    public const string RequiredPrefix = "Required. ";

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(PostPaymentRequest))
        {
            return;
        }

        foreach (PaymentField field in Enum.GetValues<PaymentField>())
        {
            schema.Properties[PaymentFieldNames.ToJsonName(field)].Description = RequiredPrefix + PaymentRequest.Messages.For(field);
        }

        schema.Properties[PaymentFieldNames.ToJsonName(PaymentField.ExpiryYear)].Description += " " + PaymentRequest.Messages.Expired;
    }
}
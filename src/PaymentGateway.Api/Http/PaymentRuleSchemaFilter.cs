using Microsoft.OpenApi.Models;

using PaymentGateway.Api.Domain;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PaymentGateway.Api.Http;

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

        foreach ((string field, OpenApiSchema property) in schema.Properties)
        {
            property.Description = RequiredPrefix + PaymentRequest.Messages.For(field);
        }

        schema.Properties[PaymentRequest.Fields.ExpiryYear].Description += " " + PaymentRequest.Messages.Expired;
    }
}
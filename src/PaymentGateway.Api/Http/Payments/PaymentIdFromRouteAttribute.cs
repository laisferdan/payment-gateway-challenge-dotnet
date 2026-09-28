using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PaymentGateway.Api.Http.Payments;

/// <summary>
/// Binds the payment id with <see cref="PaymentIdModelBinder"/>. A plain <c>[ModelBinder]</c> would make
/// the binding source "custom", which the OpenAPI document shows as a query parameter; this keeps it the
/// route (path) parameter it is.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class PaymentIdFromRouteAttribute : ModelBinderAttribute
{
    public PaymentIdFromRouteAttribute()
        : base(typeof(PaymentIdModelBinder))
    {
        BindingSource = BindingSource.Path;
    }
}
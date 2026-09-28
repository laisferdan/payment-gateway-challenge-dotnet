using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PaymentGateway.Api.Http.Payments;

/// <summary>
/// Binds the payment id from the route exactly as sent. The platform's GUID parsing ignores surrounding
/// whitespace, which would trim an otherwise invalid value into validity (Principle IX), so a value
/// with surrounding whitespace is refused like any other value that is not a GUID.
/// </summary>
public sealed class PaymentIdModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        string? value = bindingContext.ActionContext.RouteData.Values[bindingContext.ModelName] as string;
        if (value is not null && value == value.Trim() && Guid.TryParse(value, out Guid id))
        {
            bindingContext.Result = ModelBindingResult.Success(id);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, PaymentResultMapper.InvalidIdMessage);
        }

        return Task.CompletedTask;
    }
}
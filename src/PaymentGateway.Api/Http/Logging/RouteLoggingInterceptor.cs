using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Routing;

namespace PaymentGateway.Api.Http.Logging;

public sealed class RouteLoggingInterceptor : IHttpLoggingInterceptor
{
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
    {
        if (logContext.LoggingFields != HttpLoggingFields.None
            && logContext.HttpContext.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { } route })
        {
            logContext.AddParameter("Route", route);
        }

        return default;
    }

    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext)
    {
        return default;
    }
}
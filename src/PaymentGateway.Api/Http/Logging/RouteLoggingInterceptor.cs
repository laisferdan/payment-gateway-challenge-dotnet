using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Routing;

namespace PaymentGateway.Api.Http.Logging;

// Logs the route template (api/payments/{id}), never the raw path, which may hold a pasted card number.
public sealed class RouteLoggingInterceptor : IHttpLoggingInterceptor
{
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
    {
        // Adding a parameter would log an endpoint that opted out (e.g. /health).
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
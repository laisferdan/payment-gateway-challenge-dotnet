using System.Diagnostics;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using PaymentGateway.Api.Application.Ports;
using PaymentGateway.Api.Application.ProcessPayment;
using PaymentGateway.Api.Application.RetrievePayment;
using PaymentGateway.Api.Http.Logging;
using PaymentGateway.Api.Http.Payments;
using PaymentGateway.Api.Infrastructure.AcquiringBank;
using PaymentGateway.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

// Gives every request/bank-call an Activity (hence a trace id) in place of hosting diagnostics
// logging, which is off because it would put the raw path (may hold a pasted card number) in scope.
// The OTLP exporter is opt-in, so a deployment without a collector doesn't fail every export cycle.
OpenTelemetryBuilder otel = builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics.AddMeter(PaymentMetrics.MeterName).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
{
    otel.UseOtlpExporter();
}

// Avoids a duplicate "request field is required" error next to the unreadable-body error.
builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options =>
    {
        // The web defaults would accept "1050" as a number.
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
        context.HttpContext.RequestServices.GetRequiredService<InvalidModelStateResponder>().Respond(context));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml")));
builder.Services.AddHealthChecks();

// No path: it may hold a pasted card number (the interceptor adds the route template instead).
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration;
    options.CombineLogs = true;
});
builder.Services.AddHttpLoggingInterceptor<RouteLoggingInterceptor>();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<AcquiringBankOptions>()
    .BindConfiguration(AcquiringBankOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();
builder.Services.AddSingleton<PaymentMetrics>();
builder.Services.AddScoped<ProcessPaymentService>();
builder.Services.AddScoped<RetrievePaymentService>();
builder.Services.AddSingleton<PaymentResultMapper>();
builder.Services.AddSingleton<InvalidModelStateResponder>();

builder.Services.AddHttpClient<IAcquiringBank, AcquiringBankClient>((services, client) =>
{
    AcquiringBankOptions options = services.GetRequiredService<IOptions<AcquiringBankOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString());

var app = builder.Build();

// First, so it also catches exceptions thrown by routing/logging/everything below it.
app.UseExceptionHandler();
app.UseStatusCodePages();

// Before HttpLogging, so the interceptor can read the matched route.
app.UseRouting();
app.UseHttpLogging();

app.Use((context, next) =>
{
    string? traceId = Activity.Current?.TraceId.ToString();
    context.Response.OnStarting(() =>
    {
        context.Response.Headers[Program.TraceIdHeader] = traceId;
        return Task.CompletedTask;
    });
    return next(context);
});

if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

// Liveness only: a readiness probe that also called the bank could take the gateway itself out of
// rotation over a bank outage, which is worse than the outage.
app.MapHealthChecks("/health").WithHttpLogging(HttpLoggingFields.None);

app.Run();

public partial class Program
{
    public const string TraceIdHeader = "X-Trace-Id";
}
using System.Diagnostics;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Options;

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
builder.Logging.AddConsole();

// Nothing is exported: this guarantees every request and bank call an Activity, hence a trace id
// for logs, X-Trace-Id, error bodies and the traceparent sent to the bank (hosting logs are off).
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());

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
{
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml"));
    options.SupportNonNullableReferenceTypes();
});
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
})
// A followed 307/308 would re-send the card number and CVV to wherever Location points.
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Type = null;
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString();
});

var app = builder.Build();

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

app.MapHealthChecks("/health").WithHttpLogging(HttpLoggingFields.None);

app.Run();

public partial class Program
{
    public const string TraceIdHeader = "X-Trace-Id";
}
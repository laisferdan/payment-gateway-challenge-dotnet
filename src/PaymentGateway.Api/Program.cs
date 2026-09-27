using System.Diagnostics;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Application;
using PaymentGateway.Api.Http;
using PaymentGateway.Api.Infrastructure;

// Hosting diagnostics logging is off (appsettings.json) because its request scope would print the
// request path – which may hold a pasted card number – on every entry. The host then starts a
// request Activity (and so a trace id) only when its ActivitySource has a listener.
ActivitySource.AddActivityListener(new ActivityListener
{
    ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
    Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
});

var builder = WebApplication.CreateBuilder(args);

// JSON lines with scopes, so every entry carries the request's TraceId. No HTTP request logging:
// request paths and bodies can contain a pasted card number (Constitution VIII).
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
        context.HttpContext.RequestServices.GetRequiredService<PaymentResultMapper>().ToInvalidModelStateResult(context));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml")));
// Liveness only: the acquiring bank is not checked, so a bank outage does not make the gateway look dead.
builder.Services.AddHealthChecks();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<AcquiringBankOptions>()
    .BindConfiguration(AcquiringBankOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();
builder.Services.AddSingleton<PaymentGatewayMetrics>();
builder.Services.AddScoped<ProcessPaymentService>();
builder.Services.AddSingleton<PaymentResultMapper>();

// One call per payment: no retry or resilience handlers (a payment request is not idempotent).
builder.Services.AddHttpClient<IAcquiringBank, AcquiringBankClient>((services, client) =>
{
    AcquiringBankOptions options = services.GetRequiredService<IOptions<AcquiringBankOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

// The same 32-hex trace id the log scope uses (the framework default is the longer W3C id).
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString());

var app = builder.Build();

app.UseExceptionHandler();
// Body-less errors from routing (unknown route, wrong method) become ProblemDetails too.
app.UseStatusCodePages();

// Swagger is switched on by configuration, never by the Development environment, which would also
// enable the developer exception page in a container.
if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
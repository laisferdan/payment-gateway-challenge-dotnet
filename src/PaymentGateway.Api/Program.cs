using System.Diagnostics;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Application;
using PaymentGateway.Api.Http;
using PaymentGateway.Api.Infrastructure;

// Hosting diagnostics logging is off (card numbers in paths), so this listener is what gives requests a trace id.
ActivitySource.AddActivityListener(new ActivityListener
{
    ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
    Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
});

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

// Avoids a duplicate "request field is required" error next to the unreadable-body error.
builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
        context.HttpContext.RequestServices.GetRequiredService<PaymentResultMapper>().ToUnreadableBodyResult(context));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml"));
    options.SchemaFilter<PaymentRuleSchemaFilter>();
});
builder.Services.AddHealthChecks();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<AcquiringBankOptions>()
    .BindConfiguration(AcquiringBankOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();
builder.Services.AddSingleton<PaymentGatewayMetrics>();
builder.Services.AddScoped<ProcessPaymentService>();
builder.Services.AddScoped<RetrievePaymentService>();
builder.Services.AddSingleton<PaymentResultMapper>();

builder.Services.AddHttpClient<IAcquiringBank, AcquiringBankClient>((services, client) =>
{
    AcquiringBankOptions options = services.GetRequiredService<IOptions<AcquiringBankOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();

if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
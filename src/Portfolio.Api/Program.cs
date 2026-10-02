using System.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// OpenTelemetry Configuration
// ============================================================
const string serviceName = "portfolio-api";
const string serviceVersion = "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["host.name"] = Environment.MachineName
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = httpContext =>
            {
                // Ignore health checks from tracing noise
                var path = httpContext.Request.Path.Value ?? "";
                return !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
            };
        })
        .AddHttpClientInstrumentation()
        .AddSource(serviceName) // custom ActivitySource
        .SetSampler(new AlwaysOnSampler()) // for demo; use ParentBasedSampler in production
        .AddOtlpExporter(options =>
        {
            // Default: gRPC to localhost:4317 (Collector)
            options.Endpoint = new Uri(
                builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(serviceName)
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(
                builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");
        }));

// OpenTelemetry Logs
builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
    logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion));
    logging.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(
            builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");
    });
});

// ============================================================
// Application services
// ============================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Custom ActivitySource for manual spans
builder.Services.AddSingleton(new ActivitySource(serviceName));

var app = builder.Build();

// ============================================================
// Middleware pipeline
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

// Simple health endpoint (not traced)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = serviceName }));

app.Run();

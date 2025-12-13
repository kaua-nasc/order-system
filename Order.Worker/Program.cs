using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Order.Worker;
using Order.Worker.Domain;
using Order.Worker.Infra;
using Order.Worker.Infra.Database;
using Order.Worker.Observability.Metrics;
using Order.Worker.Observability.Tracing;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services
    .AddSingleton<AppTracing>()
    .AddSingleton<AppMetrics>()
    .AddSingleton<MessageProcessor>()
    .AddSingleton<RabbitMqConsumer>();

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(AppTracing.ActivitySourceName, serviceVersion: AppTracing.ActivitySourceVersion);
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("Order.Worker")
            .AddOtlpExporter((cfg, options) =>
            {
                cfg.Protocol = OtlpExportProtocol.Grpc;
                cfg.Endpoint = new Uri("http://localhost:4317");
                options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
            });
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(AppTracing.ActivitySourceName)
            .SetResourceBuilder(resourceBuilder)
            .AddAspNetCoreInstrumentation()
            .AddNpgsql()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(cfg =>
            {
                cfg.Protocol = OtlpExportProtocol.Grpc;
                cfg.Endpoint = new Uri("http://localhost:4317");
            });
    });

var host = builder.Build();
await host.RunAsync();
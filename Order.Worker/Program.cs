using System.Reflection;
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
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services
    .AddSingleton<AppTracing>()
    .AddSingleton<AppMetrics>()
    .AddSingleton<MessageProcessor>()
    .AddSingleton<RabbitMqConsumer>();

var otlpExporterEndpoint = Environment.GetEnvironmentVariable("OTLP_EXPORTER_ENDPOINT") 
    ?? throw new InvalidOperationException("OTLP_EXPORTER_ENDPOINT not set");

var serviceName = builder.Environment.ApplicationName;

var serviceVersion =
    Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? throw new InvalidOperationException();
    
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName, serviceVersion: serviceVersion, serviceInstanceId: Environment.MachineName);

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(serviceName)
            .SetResourceBuilder(resourceBuilder)
            .AddOtlpExporter((cfg, options) =>
            {
                cfg.Protocol = OtlpExportProtocol.Grpc;
                cfg.Endpoint = new Uri(otlpExporterEndpoint);
                options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000;
            });
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(serviceName)
            .SetResourceBuilder(resourceBuilder)
            .AddNpgsql()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(cfg =>
            {
                cfg.Protocol = OtlpExportProtocol.Grpc;
                cfg.Endpoint = new Uri(otlpExporterEndpoint);
            });
    });

await builder.Build().RunAsync();
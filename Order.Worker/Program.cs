using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Order.Worker.Domain;
using Order.Worker.Extensions;
using Order.Worker.Infra.Database;
using Order.Worker.Infra.MessageBroker.Consumers;
using Order.Worker.Infra.MultiTenant;
using Order.Worker.Observability.Metrics;
using Order.Worker.Observability.Tracing;
using VaultSharp.Extensions.Configuration;
using VaultSharp.V1.AuthMethods.Token;
using Winton.Extensions.Configuration.Consul;

var vaultUrl = Environment.GetEnvironmentVariable("VAULT_URL") 
    ?? throw new InvalidOperationException("VAULT_URL not set");
var vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN") 
    ?? throw new InvalidOperationException("VAULT_TOKEN not set");

var builder = Host.CreateApplicationBuilder(args);

var serviceName = builder.Environment.ApplicationName;
var serviceVersion =
    Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? throw new InvalidOperationException();

var authMethod = new TokenAuthMethodInfo(vaultToken);
builder.Configuration.AddVaultConfiguration(
    () => new VaultOptions(vaultUrl, authMethod),
    "order-system",
    "secret"
);

builder.Configuration.AddConsul(
    "tenants",
    options =>
    {
        options.ConsulConfigurationOptions = cco =>
        {
            cco.Address = new Uri(builder.Configuration.GetValueByKey<string>("Consul:Connection"));
        };
        options.Optional = true;
        options.PollWaitTime = TimeSpan.FromSeconds(5);
        options.ReloadOnChange = true;
    }
);

builder.Services
    .AddSingleton<AppTracing>()
    .AddSingleton<AppMetrics>()
    .AddScoped<MessageProcessor>();

builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<ITenantConnectionProvider,TenantConnectionProvider>();

builder.Services.AddHostedService<RabbitMqConsumer>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var connectionProvider = serviceProvider.GetRequiredService<ITenantConnectionProvider>();
    
    options.UseNpgsql(connectionProvider.GetConnectionString());
});

var otlpEndpoint = builder.Configuration.GetValueByKey<string>("OpenTelemetry:Endpoint");
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource
            .AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion, 
                serviceInstanceId: Environment.MachineName);
    })
    .WithLogging(logging =>
    {
        logging
            .AddOtlpExporter((cfg, options) =>
            {
                cfg.Protocol = OtlpExportProtocol.Grpc;
                cfg.Endpoint = new Uri(otlpEndpoint);
                options.ExportProcessorType = ExportProcessorType.Batch;
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(serviceName)
            .AddOtlpExporter((cfg, options) =>
            {
                cfg.Protocol = OtlpExportProtocol.Grpc;
                cfg.Endpoint = new Uri(otlpEndpoint);
                options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000;
            });
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(serviceName)
            .AddNpgsql()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(cfg =>
            {
                cfg.Protocol = OtlpExportProtocol.Grpc;
                cfg.Endpoint = new Uri(otlpEndpoint);
            });
    });

builder.Services.AddRabbitMqConnection(
    builder.Configuration.GetValueByKey<string>("RabbitMQ:Host"), 
    builder.Configuration.GetValueByKey<string>("RabbitMQ:User"),
    builder.Configuration.GetValueByKey<string>("RabbitMQ:Pass"));

await builder.Build().RunAsync();
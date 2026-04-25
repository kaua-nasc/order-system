using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Order.Input.Domain.Entities;
using Order.Input.Domain.Exceptions;
using Order.Input.Domain.Messages;
using Order.Input.Domain.Specification.Services;
using Order.Input.Domain.Specification.Validations;
using Order.Input.Extensions;
using Order.Input.Filters;
using Order.Input.Infra.Database;
using Order.Input.Infra.MessageBroker;
using Order.Input.Infra.MultiTenant;
using Order.Input.Middlewares;
using Order.Input.Observability.Metrics;
using Order.Input.Observability.Tracing;
using VaultSharp;
using VaultSharp.Extensions.Configuration;
using VaultSharp.V1.AuthMethods.Token;
using Winton.Extensions.Configuration.Consul;

var builder = WebApplication.CreateBuilder(args);

// 1. Carrega variáveis de ambiente básicas
var vaultUrl = builder.Configuration["VAULT_URL"] ?? throw new InvalidOperationException("VAULT_URL not set");
var vaultToken = builder.Configuration["VAULT_TOKEN"] ?? throw new InvalidOperationException("VAULT_TOKEN not set");
var vaultPath = builder.Configuration["VAULT_PATH"] ?? throw new InvalidOperationException("VAULT_PATH not set");

// 2. Configura o Cliente do Vault e injeção de configurações GLOBAIS
var authMethod = new TokenAuthMethodInfo(vaultToken);
var vaultClientSettings = new VaultClientSettings(vaultUrl, authMethod);
var vaultClient = new VaultClient(vaultClientSettings);

builder.Services.AddSingleton<IVaultClient>(vaultClient);

// Injeta as configurações do Vault no IConfiguration para que Consul/Rabbit/OTEL possam ler
builder.Configuration.AddVaultConfiguration(
    () => new VaultOptions(vaultUrl, authMethod),
    vaultPath,
    "secret"
);

// 3. Agora que o Vault carregou, configuramos o Consul
builder.Configuration.AddConsul(
    vaultPath,
    options =>
    {
        options.ConsulConfigurationOptions = cco =>
        {
            // Pega o endereço do Consul que veio do Vault
            cco.Address = new Uri(builder.Configuration.GetValueByKey<string>("Consul:Connection"));
        };
        options.Optional = true;
        options.PollWaitTime = TimeSpan.FromSeconds(5);
        options.ReloadOnChange = true;
    }
);

// 4. Configuração de Serviços
builder.Services.AddOpenApi();

builder.Services
    .AddSingleton<AppTracing>()
    .AddSingleton<AppMetrics>()
    .AddDbContext<AppDbContext>((serviceProvider, options) =>
    {
        var databaseProvider = serviceProvider.GetRequiredService<ITenantDatabaseProvider>();
        options.UseNpgsql(databaseProvider.GetConnectionString());
    })
    .AddScoped<RabbitMqPublisher>()
    .AddScoped<TenantService>()
    .AddScoped<ITenantDatabaseProvider, TenantDatabaseProvider>()
    .AddHostedService<PublisherInitializerHostedService>()
    .AddScoped<Validator<OrderEntity>>()
    .AddSpecificationsFromAssemblyContaining<OrderSpecification>();

// 5. OpenTelemetry (Lendo chaves do Vault)
var serviceName = builder.Environment.ApplicationName;
var serviceVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
var otlpEndpoint = builder.Configuration.GetValueByKey<string>("OpenTelemetry:Endpoint");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: serviceVersion, serviceInstanceId: Environment.MachineName))
    .WithLogging(logging => logging.AddOtlpExporter((cfg, _) => { cfg.Protocol = OtlpExportProtocol.Grpc; cfg.Endpoint = new Uri(otlpEndpoint); }))
    .WithMetrics(metrics => metrics.AddMeter(serviceName).AddOtlpExporter((cfg, _) => { cfg.Protocol = OtlpExportProtocol.Grpc; cfg.Endpoint = new Uri(otlpEndpoint); }))
    .WithTracing(tracing => tracing
        .AddSource(serviceName)
        .AddAspNetCoreInstrumentation(options => 
        {
            options.RecordException = true; // Captura a exceção no Span
        })
        .AddHttpClientInstrumentation()
        .AddNpgsql()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(cfg => { cfg.Protocol = OtlpExportProtocol.Grpc; cfg.Endpoint = new Uri(otlpEndpoint); }));

// 6. RabbitMQ (Lendo chaves do Vault)
var rabbitHost = builder.Configuration.GetValueByKey<string>("RabbitMQ:Host");
var rabbitUser = builder.Configuration.GetValueByKey<string>("RabbitMQ:User");
var rabbitPass = builder.Configuration.GetValueByKey<string>("RabbitMQ:Pass");
builder.Services.AddRabbitMqConnection(rabbitHost, rabbitUser, rabbitPass);

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseHttpsRedirection();
app.UseMiddleware<TenantMiddleware>();

app.MapPost("/register/order",
    async Task<IResult> (ILogger<Program> logger, AppTracing tracer, AppMetrics metrics, RabbitMqPublisher publisher, AppDbContext context, [FromBody] OrderEntity order) =>
    {
        using var act = tracer.StartActivity("RegisterOrder", ActivityKind.Producer);
        try
        {
            metrics.IncrementOrderCounter();
            var exists = await context.Orders.FindAsync(order.OrderId);
            if (exists is not null)
            {
                throw new AlreadyExistsException("this order already exists");
            }
            
            await context.Orders.AddAsync(order);
            await context.SaveChangesAsync();
            await publisher.PublishAsync(new OrderMessage(order));
            logger.LogInformation("Order {OrderId} registered and published successfully", order.OrderId);
            act?.SetStatus(ActivityStatusCode.Ok);
            return Results.Created($"/orders/{order.OrderId}", new { id = order.OrderId, status = "processing" });
        }
        catch (Exception ex)
        {
            act?.AddException(ex);
            act?.SetStatus(ActivityStatusCode.Error, ex.Message);
            metrics.IncrementOrderErrorCounter();

            logger.LogError(ex, "Error while processing order {OrderId}", order.OrderId);
            return Results.Problem(title: "Order processing failed", statusCode: 500);
        }
    }).AddEndpointFilter<ValidationFilter<OrderEntity>>();

await app.RunAsync();

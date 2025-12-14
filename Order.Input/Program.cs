using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Order.Input.Domain.Messages;
using Order.Input.Domain.Specification.Services;
using Order.Input.Domain.Specification.Validations;
using Order.Input.Domain.ValueObjects;
using Order.Input.Extensions;
using Order.Input.Filters;
using Order.Input.Infra.MessageBroker;
using Order.Input.Observability.Metrics;
using Order.Input.Observability.Tracing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services
    .AddSingleton<AppTracing>()
    .AddSingleton<AppMetrics>()
    .AddSingleton<RabbitMqPublisher>()
    .AddHostedService<PublisherInitializerHostedService>()
    .AddScoped<Validator<OrderValueObject>>()
    .AddSpecificationsFromAssemblyContaining<OrderSpecification>();

var otlpExporterEndpoint = Environment.GetEnvironmentVariable("OTLP_EXPORTER_ENDPOINT") 
    ?? throw new InvalidOperationException("OTLP_EXPORTER_ENDPOINT not set");

var serviceName = builder.Environment.ApplicationName;
var serviceVersion =
    Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? throw new InvalidOperationException();

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
                cfg.Endpoint = new Uri(otlpExporterEndpoint);
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
                cfg.Endpoint = new Uri(otlpExporterEndpoint);
                options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
            });
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(serviceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddNpgsql()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(cfg =>
            {
                cfg.Protocol = OtlpExportProtocol.Grpc;
                cfg.Endpoint = new Uri(otlpExporterEndpoint);
            });
    });

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/register/order",
    async Task<IResult> (
        ILogger<Program> logger,
        AppTracing tracer,
        AppMetrics metrics,
        RabbitMqPublisher publisher,
        [FromBody] OrderValueObject order) =>
    {
        using var act = tracer.StartActivity("RegisterOrder", ActivityKind.Producer);

        try
        {
            metrics.IncrementOrderCounter();

            var message = new OrderMessage(order);
            await publisher.PublishAsync(message);

            logger.LogInformation("Order {OrderId} registered successfully", order.OrderId);

            act?.SetStatus(ActivityStatusCode.Ok);
            
            return Results.Created(
                $"/orders/{order.OrderId}",
                new
                {
                    id = order.OrderId,
                    status = "processing",
                    estimatedCompletionTime = DateTime.UtcNow.AddMinutes(5),
                    _links = new
                    {
                        self = $"/orders/{order.OrderId}",
                        status = $"/orders/{order.OrderId}/status",
                        cancel = $"/orders/{order.OrderId}/cancel"
                    }
                }
            );
        }
        catch (Exception ex)
        {
            act?.SetStatus(ActivityStatusCode.Error, ex.Message);
            metrics.IncrementOrderErrorCounter();

            logger.LogError(ex, "Error while publishing order {OrderId}", order.OrderId);
            return Results.Problem(
                title: "Order processing failed",
                detail: "An error occurred while processing your order. Please try again later.",
                statusCode: StatusCodes.Status500InternalServerError,
                instance: $"/orders/errors/{Guid.NewGuid()}"
            );
        }
    }).AddEndpointFilter<ValidationFilter<OrderValueObject>>();

await app.RunAsync();
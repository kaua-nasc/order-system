using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpenTelemetry.Exporter;
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
using Order.Input.Tracing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddSingleton<AppTracing>();
builder.Services.AddSingleton<AppMetrics>();
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<PublisherInitializerHostedService>();
builder.Services.AddScoped<Validator<OrderValueObject>>();
builder.Services.AddSpecificationsFromAssemblyContaining<OrderSpecification>();

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(AppTracing.ActivitySourceName, serviceVersion: AppTracing.ActivitySourceVersion);
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("Order.Input")
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
            .AddHttpClientInstrumentation()
            .AddNpgsql()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(cfg =>
            {
                cfg.Protocol = OtlpExportProtocol.Grpc;
                cfg.Endpoint = new Uri("http://localhost:4317");
            })
            .AddConsoleExporter();
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
        IMessagePublisher publisher,
        [FromBody] OrderValueObject order) =>
    {
        using var act = tracer.StartActivity("RegisterOrder", ActivityKind.Producer);

        try
        {
            metrics.IncrementOrderCounter();

            var message = new OrderMessage(order);
            await publisher.PublishAsync(message);

            logger.LogInformation("Order {OrderId} registered successfully", order.OrderId);

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
        finally
        {
            act?.SetStatus(ActivityStatusCode.Ok);
        }
    }).AddEndpointFilter<ValidationFilter<OrderValueObject>>();

await app.RunAsync();
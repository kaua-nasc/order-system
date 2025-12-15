using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Order.Worker.Domain.Messages;
using Order.Worker.Domain.ValueObjects;
using Order.Worker.Infra.Database;
using Order.Worker.Observability.Metrics;
using Order.Worker.Observability.Tracing;

namespace Order.Worker.Domain;

public class MessageProcessor(ILogger<MessageProcessor> logger, AppTracing tracer, AppMetrics metrics, AppDbContext context)
{
    public async Task Process(OrderMessage message)
    {
        var stopwatch = Stopwatch.StartNew();
        metrics.IncrementActiveProcessing();

        try
        {
            metrics.IncrementMessagesConsumed();
            using var activity = tracer.Source.StartActivity(nameof(MessageProcessor), ActivityKind.Consumer);

            activity?.SetTag("message.type", nameof(OrderMessage));
            activity?.SetTag("processing.step", "start");
            activity?.SetTag("message.content", message);
            activity?.SetTag("thread.id", Environment.CurrentManagedThreadId);

            var exists = await context.OrdersProcessed
                .AnyAsync(order => order.OrderId == message.OrderId);

            if (exists)
            {
                metrics.IncrementDuplicateMessages();
                logger.LogDebug("Duplicate order {OrderId}", message.OrderId);
                return;
            }

            var orderProcessed = new OrderProcessedValueObject(message);
            await context.OrdersProcessed.AddAsync(orderProcessed);
            await context.SaveChangesAsync();

            metrics.IncrementOrdersProcessed();
            metrics.RecordOrderValue(message.TotalAmount);
            metrics.RecordOrderByValueRange(message.TotalAmount);

            logger.LogInformation("Order {OrderId} processed", message.OrderId);
        }
        catch (Exception ex)
        {
            metrics.IncrementProcessingErrors();
            logger.LogError(ex, "Error while processing order {OrderId}", message.OrderId);
            throw;
        }
        finally
        {
            metrics.DecrementActiveProcessing();
            stopwatch.Stop();
            metrics.RecordProcessingDuration(stopwatch.Elapsed);
        }
    }
}
using System.Diagnostics;
using Order.Worker.Domain.Entities;
using Order.Worker.Domain.Exceptions;
using Order.Worker.Domain.Messages;
using Order.Worker.Infra.Database;
using Order.Worker.Infra.MultiTenant;
using Order.Worker.Observability.Metrics;
using Order.Worker.Observability.Tracing;

namespace Order.Worker.Domain;

public class MessageProcessor(ILogger<MessageProcessor> logger, IAppTracing tracer, IAppMetrics metrics, AppDbContext context, TenantService tenantService)
{
    public async Task Process(OrderMessage message)
    {
        var tenantId = tenantService.TenantId ?? "unknown";
        var stopwatch = Stopwatch.StartNew();
        metrics.IncrementActiveProcessing(tenantId);
        OrderEntity? order = null;

        try
        {
            using var activity = tracer.StartActivity(nameof(MessageProcessor), ActivityKind.Consumer);

            activity?.SetTag("message.type", nameof(OrderMessage));
            activity?.SetTag("processing.step", "start");
            activity?.SetTag("message.content", message);
            activity?.SetTag("thread.id", Environment.CurrentManagedThreadId);
            
            order = await context.Orders
                .FindAsync(message.OrderId);

            if (order is null)
            {
                throw new NotFoundException($"order with id {message.OrderId} not found");
            }
            
            order.MarkAsProcessed();
            
            await context.SaveChangesAsync();
            
            metrics.IncrementOrdersProcessed(tenantId);
            metrics.RecordOrderValue(order.TotalAmount, tenantId);
            metrics.RecordOrderByValueRange(order.TotalAmount, tenantId);

            logger.LogInformation("Order {OrderId} processed", message.OrderId);
            metrics.IncrementMessagesConsumed(tenantId);
        }
        catch (NotFoundException)
        {
            metrics.IncrementProcessingErrors(tenantId);
            logger.LogError("Order {OrderId} not found", message.OrderId);
            throw;
        }
        catch (Exception ex)
        {
            if (order is not null)
            {
                try
                {
                    order.MarkAsFailed();
                    await context.SaveChangesAsync();
                    logger.LogWarning("Order {OrderId} marked as failed due to an error.", message.OrderId);
                }
                catch (Exception dbEx)
                {
                    logger.LogError(dbEx, "Failed to update order {OrderId} status to Error", message.OrderId);
                }
            }

            metrics.IncrementProcessingErrors(tenantId);
            logger.LogError(ex, "Error while processing order {OrderId}", message.OrderId);
            throw;
        }
        finally
        {
            metrics.DecrementActiveProcessing(tenantId);
            stopwatch.Stop();
            metrics.RecordProcessingDuration(stopwatch.Elapsed, tenantId);
        }
    }
}

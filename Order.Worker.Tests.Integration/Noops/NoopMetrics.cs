using Order.Worker.Observability.Metrics;

namespace Order.Worker.Tests.Integration.Noops;

public sealed class NoopMetrics : IAppMetrics
{
    public void IncrementMessagesConsumed(string tenantId) { }
    public void IncrementDuplicateMessages(string tenantId) { }
    public void IncrementOrdersProcessed(string tenantId) { }
    public void IncrementProcessingErrors(string tenantId) { }

    public void RecordOrderValue(decimal amount, string tenantId) { }

    public void RecordProcessingDuration(TimeSpan duration, string tenantId) { }

    public void IncrementActiveProcessing(string tenantId) { }
    public void DecrementActiveProcessing(string tenantId) { }

    public void RecordOrderByValueRange(decimal amount, string tenantId) { }
}
using Order.Worker.Infra.MultiTenant;

namespace Order.Worker.Tests.Integration.Noops;

public sealed class StaticTenantDatabaseProvider(string connectionString) : ITenantDatabaseProvider
{
    public string GetConnectionString() => connectionString;
}
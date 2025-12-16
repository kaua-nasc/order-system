using Order.Worker.Infra.MultiTenant;

namespace Order.Worker.Tests.Integration.Noops;

public sealed class StaticTenantConnectionProvider(string connectionString) : ITenantConnectionProvider
{
    public string GetConnectionString() => connectionString;
}
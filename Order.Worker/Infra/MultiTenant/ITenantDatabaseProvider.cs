namespace Order.Worker.Infra.MultiTenant;

public interface ITenantDatabaseProvider
{
    string GetConnectionString();
}

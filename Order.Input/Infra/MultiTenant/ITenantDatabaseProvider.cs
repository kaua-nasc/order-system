namespace Order.Input.Infra.MultiTenant;

public interface ITenantDatabaseProvider
{
    string GetConnectionString();
}

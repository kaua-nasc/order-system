namespace Order.Worker.Infra.MultiTenant;

public interface ITenantConnectionProvider
{
    string GetConnectionString();
}
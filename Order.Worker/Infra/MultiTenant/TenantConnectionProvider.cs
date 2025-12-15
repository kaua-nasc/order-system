using Npgsql;

namespace Order.Worker.Infra.MultiTenant;

public class TenantConnectionProvider(TenantService tenantService, IConfiguration configuration) : ITenantConnectionProvider
{
    public string GetConnectionString()
    {
        var tenantId = tenantService.TenantId;

        if (string.IsNullOrEmpty(tenantId))
        {
            throw new InvalidOperationException("TenantID não identificado no contexto atual.");
        }

        // A estrutura no Consul será: tenants/{tenantId}/db_name
        var dbName = configuration[$"{tenantId}:db_name"];

        if (string.IsNullOrEmpty(dbName))
        {
            throw new InvalidOperationException($"Tenant '{tenantId}' não encontrado no Consul ou sem banco configurado.");
        }

        var connectionTemplate = configuration.GetValue<string>("Postgres:Connection");
        
        if (string.IsNullOrEmpty(connectionTemplate))
            throw new InvalidOperationException("Connection String de Template não encontrada.");

        var builder = new NpgsqlConnectionStringBuilder(connectionTemplate)
        {
            Database = dbName
        };

        return builder.ConnectionString;
    }
}
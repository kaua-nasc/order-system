using System.Text.Json;
using Npgsql;
using VaultSharp;

namespace Order.Worker.Infra.MultiTenant;

public class TenantDatabaseProvider(
    TenantService tenantService, 
    IConfiguration configuration, 
    IVaultClient vaultClient) : ITenantDatabaseProvider
{
    public string GetConnectionString()
    {
        var tenantId = tenantService.TenantId;

        if (string.IsNullOrEmpty(tenantId))
        {
            throw new InvalidOperationException("TenantID não identificado no contexto atual.");
        }

        var basePath = configuration["VAULT_PATH"];
        var fullPath = $"{basePath}/{tenantId}";

        try
        {
            var secret = vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(fullPath, mountPoint: "secret")
                .GetAwaiter()
                .GetResult();

            if (secret?.Data?.Data != null && secret.Data.Data.TryGetValue("Postgres", out var postgresValue))
            {
                // Se o valor for um JsonElement (padrão do System.Text.Json usado pelo VaultSharp)
                if (postgresValue is JsonElement element && element.TryGetProperty("Connection", out var connectionProp))
                {
                    return connectionProp.GetString()!;
                }

                // Fallback para outros tipos de mapeamento de objetos da lib
                var jsonStr = postgresValue.ToString();
                using var doc = JsonDocument.Parse(jsonStr!);
                if (doc.RootElement.TryGetProperty("Connection", out var prop))
                {
                    return prop.GetString()!;
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Erro ao buscar conexão no Vault para o tenant '{tenantId}' no path '{fullPath}'.", ex);
        }

        throw new InvalidOperationException($"String de conexão 'Postgres:Connection' não encontrada no Vault para o tenant '{tenantId}' no path '{fullPath}'.");
    }
}
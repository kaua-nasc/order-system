using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Order.Input.Infra.MultiTenant;
using VaultSharp.Extensions.Configuration;
using VaultSharp.V1.AuthMethods.Token;

namespace Order.Input.Infra.Database;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // 1. Try to get the connection directly from the --connection command
        string? connectionString = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--connection" && i + 1 < args.Length)
            {
                connectionString = args[i + 1];
                break;
            }
        }

        // Determine the correct base path (handle root vs project folder)
        var basePath = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(basePath, "appsettings.json")) && Directory.Exists(Path.Combine(basePath, "Order.Input")))
        {
            basePath = Path.Combine(basePath, "Order.Input");
        }

        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        // 2. If not provided via command, try to load from environment or launchSettings
        if (string.IsNullOrEmpty(connectionString))
        {
            var vaultUrl = Environment.GetEnvironmentVariable("VAULT_URL");
            var vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");
            var vaultTenantValue = Environment.GetEnvironmentVariable("VAULT_TENANT_VALUE");
            var vaultPath = Environment.GetEnvironmentVariable("VAULT_PATH") ?? "order-system";

            if (string.IsNullOrEmpty(vaultUrl) || string.IsNullOrEmpty(vaultToken))
            {
                try
                {
                    var launchSettingsPath = Path.Combine(basePath, "Properties", "launchSettings.json");
                    if (File.Exists(launchSettingsPath))
                    {
                        using var file = File.OpenRead(launchSettingsPath);
                        using var json = JsonDocument.Parse(file);

                        var profiles = json.RootElement.GetProperty("profiles");
                        foreach (var profile in profiles.EnumerateObject())
                        {
                            if (!profile.Value.TryGetProperty("environmentVariables", out var envVars)) continue;
                            
                            if (string.IsNullOrEmpty(vaultUrl) && envVars.TryGetProperty("VAULT_URL", out var vUrl))
                                vaultUrl = vUrl.GetString();

                            if (string.IsNullOrEmpty(vaultToken) && envVars.TryGetProperty("VAULT_TOKEN", out var vToken))
                                vaultToken = vToken.GetString();
                            
                            if (string.IsNullOrEmpty(vaultTenantValue) && envVars.TryGetProperty("VAULT_TENANT_VALUE", out var vTenantValue))
                                vaultTenantValue = vTenantValue.GetString();

                            if (envVars.TryGetProperty("VAULT_PATH", out var vPath))
                                vaultPath = vPath.GetString();
                        }
                    }
                }
                catch { /* Ignore design-time errors */ }
            }

            if (!string.IsNullOrEmpty(vaultUrl) && !string.IsNullOrEmpty(vaultToken))
            {
                try 
                {
                    // Hardcoded mount point as requested: "secret"
                    var mountPoint = "secret";
                    
                    // The full path is the combination of VAULT_PATH and VAULT_TENANT_VALUE
                    var secretPath = vaultPath;
                    if (!string.IsNullOrEmpty(vaultTenantValue))
                    {
                        secretPath = $"{secretPath.TrimEnd('/')}/{vaultTenantValue}";
                    }

                    Console.WriteLine($"[Debug] Tentando Vault: URL={vaultUrl}, Mount={mountPoint}, Path={secretPath}");

                    var authMethod = new TokenAuthMethodInfo(vaultToken);
                    builder.AddVaultConfiguration(
                        () => new VaultOptions(vaultUrl, authMethod),
                        secretPath, 
                        mountPoint
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Erro] Configuração Vault: {ex.Message}");
                }
            }

            var configuration = builder.Build();
            
            // Debug: Listar todas as chaves carregadas (opcional, mas ajuda)
            // Console.WriteLine("[Debug] Chaves carregadas: " + string.Join(", ", configuration.AsEnumerable().Select(x => x.Key)));

            connectionString = configuration.GetValue<string>("Postgres:Connection")
                 ?? configuration.GetConnectionString("DatabaseTemplate");
        }

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception($"Missing connection string. Looked in: {basePath}. Use --connection or check Vault/Env config.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options, new DesignTimeConnectionProvider());
    }

    private class DesignTimeConnectionProvider : ITenantDatabaseProvider
    {
        public string GetConnectionString() => ""; 
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Order.Worker.Infra.MultiTenant;
using VaultSharp.Extensions.Configuration;
using VaultSharp.V1.AuthMethods.Token;

namespace Order.Worker.Infra.Database;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        var vaultUrl = Environment.GetEnvironmentVariable("VAULT_URL");
        var vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");

        if (string.IsNullOrEmpty(vaultUrl) || string.IsNullOrEmpty(vaultToken))
        {
            try
            {
                var launchSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "Properties", "launchSettings.json");
                if (File.Exists(launchSettingsPath))
                {
                    using var file = File.OpenRead(launchSettingsPath);
                    using var json = JsonDocument.Parse(file);
                    var profiles = json.RootElement.GetProperty("profiles");
                    foreach (var profile in profiles.EnumerateObject())
                    {
                        if (profile.Value.TryGetProperty("environmentVariables", out var envVars))
                        {
                            if (string.IsNullOrEmpty(vaultUrl) && envVars.TryGetProperty("VAULT_URL", out var vUrl))
                                vaultUrl = vUrl.GetString();
                            if (string.IsNullOrEmpty(vaultToken) && envVars.TryGetProperty("VAULT_TOKEN", out var vToken))
                                vaultToken = vToken.GetString();
                        }
                    }
                }
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(vaultUrl) && !string.IsNullOrEmpty(vaultToken))
        {
            try 
            {
                var authMethod = new TokenAuthMethodInfo(vaultToken);
                builder.AddVaultConfiguration(
                    () => new VaultOptions(vaultUrl, authMethod),
                    "order-system", 
                    "secret"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Aviso] Falha Vault DesignTime: {ex.Message}");
            }
        }

        var configuration = builder.Build();
        var connectionString = configuration.GetValue<string>("Postgres:Connection")
             ?? configuration.GetConnectionString("DatabaseTemplate"); 

        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = "Host=localhost;Database=design_time_db;Username=postgres;Password=postgres";
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options, new DesignTimeDatabaseProvider());
    }

    private class DesignTimeDatabaseProvider : ITenantDatabaseProvider
    {
        public string GetConnectionString() => ""; 
    }
}

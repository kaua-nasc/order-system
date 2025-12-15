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

        // 2. Recupera a string de conexão
        // ATENÇÃO: Se no Vault estiver "Database=TEMPLATE", as migrations serão criadas
        // baseadas nesse template. Isso é normal.
        var connectionString = configuration.GetValue<string>("Postgres:Connection")
             ?? configuration.GetConnectionString("DatabaseTemplate"); // Fallback para o template novo

        if (string.IsNullOrEmpty(connectionString))
        {
            // Fallback hardcoded para dev local caso tudo falhe, só para gerar a migration
            connectionString = "Host=localhost;Database=design_time_db;Username=postgres;Password=postgres";
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        // 3. A CORREÇÃO ESTÁ AQUI:
        // Passamos um Provider "Falso" (Dummy) pois o construtor exige,
        // mas ele não será usado porque optionsBuilder já está configurado acima.
        return new AppDbContext(optionsBuilder.Options, new DesignTimeConnectionProvider());
    }

    // Classe privada "Dummy" apenas para enganar o compilador
    private class DesignTimeConnectionProvider : ITenantConnectionProvider
    {
        public string GetConnectionString() 
        {
            // Nunca deve ser chamado em tempo de design se o OnConfiguring checar IsConfigured
            return ""; 
        }
    }
}
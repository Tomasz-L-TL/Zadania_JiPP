using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Week04.Api.Data;

// Fabryka design-time zapobiega uruchomieniu startupowego seeda podczas dotnet ef.
public sealed class ProductsDbContextFactory : IDesignTimeDbContextFactory<ProductsDbContext>
{
    public ProductsDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        // Pliki appsettings trafiają do folderu z binarkami przy build.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<ProductsDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("ProductsDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Brak ConnectionStrings:ProductsDb. Ustaw ConnectionStrings__ProductsDb " +
                "(Production) lub User Secrets (Development).");
        }

        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new ProductsDbContext(options);
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Week09.Api.Data;

namespace Week09.IntegrationTests;

/// <summary>
/// Każda instancja posiada oddzielne fizyczne połączenie SQLite :memory:.
/// Utrzymujemy je otwarte przez cały czas życia fabryki — inaczej DB znika.
/// </summary>
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    // To jest jawnie testowy, nieprodukcyjny klucz do JWT.
    private const string TestJwtKey = "week09-test-only-key-not-for-production-12345678901234567890";
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(FindApiRoot());

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Data Source=:memory:",
                ["Database:InitializeOnStartup"] = "true",
                ["Jwt:Issuer"] = "Week09.Api",
                ["Jwt:Audience"] = "Week09.Client",
                ["Jwt:SigningKey"] = TestJwtKey
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<Week09DbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<Week09DbContext>>();

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            services.AddDbContext<Week09DbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection?.Dispose();
    }

    private static string FindApiRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null;
             current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName,
                "week09", "src", "Week09.Api");
            if (File.Exists(Path.Combine(candidate, "Week09.Api.csproj"))) return candidate;
        }

        throw new DirectoryNotFoundException("Nie znaleziono katalogu week09/src/Week09.Api.");
    }
}

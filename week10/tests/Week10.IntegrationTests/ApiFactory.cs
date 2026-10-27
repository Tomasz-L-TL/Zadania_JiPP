using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Week10.Api.Data;

namespace Week10.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Otwarty przez cały czas trwania hosta connection = żywa baza SQLite in-memory.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public const string AdminUsername = "test_admin";
    public const string AdminPassword = "OnlyTestPassword_12345";
    private const string TestSigningKey = "OnlyForTestFixtures_NotForUseInProduction_2026_ABCDEF";

    public ApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:Issuer"] = "Week10.Api",
                ["Jwt:Audience"] = "Week10.Api.Client",
                ["ConnectionStrings:AppDb"] = "Data Source=:memory:",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Database:SeedDemoData"] = "true",
                ["DevelopmentSeed:AdminUsername"] = AdminUsername,
                ["DevelopmentSeed:AdminEmail"] = "admin@example.test",
                ["DevelopmentSeed:AdminPassword"] = AdminPassword
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

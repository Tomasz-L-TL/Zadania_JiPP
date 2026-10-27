using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Week04.Api.Data;
using Week04.Api.Domain;

namespace Week04.Api.Seeding;

public static class DevelopmentDataSeeder
{
    private static readonly DateTime DemoTimestamp =
        new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    // Stałe SKU i data -> powtarzalny seed. Brak powtórek dzięki unikatowemu indeksowi.
    public static async Task SeedAsync(
        ProductsDbContext db, IHostEnvironment environment, CancellationToken ct = default)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Dane demonstracyjne mogą być inicjalizowane wyłącznie w Development.");
        }

        var existingSkus = (await db.Products.AsNoTracking()
                .Select(product => product.Sku)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var demos = new[]
        {
            new Product("DEMO-001", "Klawiatura", 149.99m,
                "Klawiatura mechaniczna", DemoTimestamp),
            new Product("DEMO-002", "Mysz", 79.90m,
                "Mysz bezprzewodowa", DemoTimestamp),
            new Product("DEMO-003", "Monitor", 899.00m,
                "Monitor 27 cali", DemoTimestamp)
        };

        foreach (var product in demos)
        {
            if (existingSkus.Add(product.Sku))
                db.Products.Add(product);
        }

        await db.SaveChangesAsync(ct);
    }
}

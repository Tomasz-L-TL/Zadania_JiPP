using Microsoft.EntityFrameworkCore;
using Week04.Api.Data;
using Week04.Api.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Jedno źródło konfiguracji: appsettings, User Secrets (Development), zmienne środowiskowe.
// Production nie ma connection stringa w repo — konfiguracja z zewnątrz jest obowiązkowa.
var connectionString = builder.Configuration.GetConnectionString("ProductsDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Brak ConnectionStrings:ProductsDb. Ustaw ConnectionStrings__ProductsDb " +
        "jako zmienną środowiskową lub użyj User Secrets w Development.");
}

builder.Services.AddDbContext<ProductsDbContext>(options =>
    options.UseSqlite(connectionString));

var app = builder.Build();

// Migracje automatyczne i przykładowe dane są dostępne tylko w Development.
// W Production migrację wykonuje administrator / proces wdrożeniowy (dotnet ef database update).
if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue<bool>("Database:SeedDemoData"))
    {
        await DevelopmentDataSeeder.SeedAsync(db, app.Environment);
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var products = app.MapGroup("/api/products").WithTags("Products");

products.MapGet("/", async (ProductsDbContext db, CancellationToken ct) =>
{
    var result = await db.Products.AsNoTracking()
        .OrderBy(product => product.Id)
        .Select(product => new ProductResponse(
            product.Id, product.Sku, product.Name, product.Description,
            product.Price, product.CreatedAtUtc))
        .ToListAsync(ct);

    return Results.Ok(result);
});

products.MapGet("/{id:int}", async (int id, ProductsDbContext db, CancellationToken ct) =>
{
    var product = await db.Products.AsNoTracking()
        .Where(item => item.Id == id)
        .Select(item => new ProductResponse(
            item.Id, item.Sku, item.Name, item.Description,
            item.Price, item.CreatedAtUtc))
        .FirstOrDefaultAsync(ct);

    return product is null ? Results.NotFound() : Results.Ok(product);
});

app.Run();

public sealed record ProductResponse(
    int Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    DateTime CreatedAtUtc);

public partial class Program { }

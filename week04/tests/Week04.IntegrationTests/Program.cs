using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Week04.Api.Data;
using Week04.Api.Domain;
using Week04.Api.Seeding;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Migracja jest zarejestrowana", async () => await WithDatabase(async db =>
    {
        var migrations = await db.Database.GetMigrationsAsync();
        Check(migrations.Contains("202610250001_InitialCreate"), "Nie znaleziono InitialCreate.");
    }, migrate: false)),
    ("MigrateAsync tworzy bazę i zapisuje historię migracji", () => WithDatabase(async db =>
    {
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Check(applied.Contains("202610250001_InitialCreate"), "Historia migracji jest pusta.");
    })),
    ("Po migracji nie ma oczekujących migracji", () => WithDatabase(async db =>
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Oczekujące migracje."))),
    ("Migracja tworzy tabelę Products", () => WithDatabase(async db =>
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Products'";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync());
        Check(count == 1, "Nie znaleziono tabeli Products.");
        await db.Database.CloseConnectionAsync();
    })),
    ("Migracja tworzy unikatowy indeks SKU", () => WithDatabase(async db =>
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_Products_Sku'";
        Check(Convert.ToInt64(await command.ExecuteScalarAsync()) == 1, "Brak indeksu SKU.");
        await db.Database.CloseConnectionAsync();
    })),
    ("Pusty Products bez seeda", () => WithDatabase(async db =>
        Check(await db.Products.CountAsync() == 0, "Migracja nie powinna dodawać danych."))),
    ("Klucz główny Product.Id", () => WithDatabase(db =>
    {
        var entity = db.Model.FindEntityType(typeof(Product))!;
        Check(entity.FindPrimaryKey()!.Properties.Single().Name == nameof(Product.Id), "Zły klucz.");
        return Task.CompletedTask;
    })),
    ("SKU wymagane, maks. 32 znaki, unikatowe", () => WithDatabase(db =>
    {
        var entity = db.Model.FindEntityType(typeof(Product))!;
        var sku = entity.FindProperty(nameof(Product.Sku))!;
        Check(!sku.IsNullable && sku.GetMaxLength() == 32, "Zła konfiguracja SKU.");
        Check(entity.GetIndexes().Any(index => index.IsUnique &&
            index.Properties.Count == 1 && index.Properties[0].Name == nameof(Product.Sku)),
            "SKU nie jest unikatowe.");
        return Task.CompletedTask;
    })),
    ("Name wymagane i maks. 120 znaków", () => WithDatabase(db =>
    {
        var prop = db.Model.FindEntityType(typeof(Product))!.FindProperty(nameof(Product.Name))!;
        Check(!prop.IsNullable && prop.GetMaxLength() == 120, "Zła konfiguracja Name.");
        return Task.CompletedTask;
    })),
    ("Description opcjonalne i maks. 500 znaków", () => WithDatabase(db =>
    {
        var prop = db.Model.FindEntityType(typeof(Product))!.FindProperty(nameof(Product.Description))!;
        Check(prop.IsNullable && prop.GetMaxLength() == 500, "Zła konfiguracja Description.");
        return Task.CompletedTask;
    })),
    ("Price wymagane, precyzja 18, skala 2", () => WithDatabase(db =>
    {
        var prop = db.Model.FindEntityType(typeof(Product))!.FindProperty(nameof(Product.Price))!;
        Check(!prop.IsNullable && prop.GetPrecision() == 18 && prop.GetScale() == 2,
            "Zła konfiguracja ceny.");
        return Task.CompletedTask;
    })),
    ("Zapis i odczyt z fizycznej bazy SQLite", () => WithDatabase(async db =>
    {
        db.Products.Add(new Product("X-001", "Testowy produkt", 123.45m));
        await db.SaveChangesAsync();
        var originalConnection = db.Database.GetDbConnection().ConnectionString;
        var freshOptions = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseSqlite(originalConnection).Options;
        await using var secondContext = new ProductsDbContext(freshOptions);
        var loaded = await secondContext.Products.AsNoTracking().SingleAsync();
        Check(loaded.Sku == "X-001" && loaded.Price == 123.45m,
            "Dane nie przetrwały zmiany kontekstu.");
    })),
    ("Unikatowość SKU egzekwowana przez bazę", () => WithDatabase(async db =>
    {
        db.Products.Add(new Product("AB-123", "Pierwszy", 1m));
        await db.SaveChangesAsync();
        db.Products.Add(new Product("ab-123", "Drugi", 2m));
        await ExpectThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    })),
    ("Konstruktor odrzuca błędne produkty", () => WithDatabase(db =>
    {
        ExpectThrows<ArgumentException>(() => new Product("", "Poprawna nazwa", 1m));
        ExpectThrows<ArgumentException>(() => new Product("K-1", " ", 1m));
        ExpectThrows<ArgumentOutOfRangeException>(() => new Product("K-1", "Nazwa", -1m));
        return Task.CompletedTask;
    })),
    ("Cena jest zaokrąglana do dwóch miejsc", () => WithDatabase(db =>
    {
        Check(new Product("P-1", "Cena", 12.345m).Price == 12.35m,
            "Brak zaokrąglenia kwoty.");
        return Task.CompletedTask;
    })),
    ("Dev seed dodaje dokładnie trzy produkty", () => WithDatabase(async db =>
    {
        await DevelopmentDataSeeder.SeedAsync(db, new FakeEnvironment("Development"));
        Check(await db.Products.CountAsync() == 3, "Seed nie dodał trzech rekordów.");
    })),
    ("Dev seed jest deterministyczny", () => WithDatabase(async db =>
    {
        await DevelopmentDataSeeder.SeedAsync(db, new FakeEnvironment("Development"));
        var products = await db.Products.OrderBy(product => product.Sku).ToArrayAsync();
        Check(products.Select(p => p.Sku).SequenceEqual(new[] { "DEMO-001", "DEMO-002", "DEMO-003" }),
            "Nieprawidłowe SKU.");
        Check(products[0].Price == 149.99m && products[1].Price == 79.90m &&
              products[2].Price == 899.00m, "Dane seeda zostały zmienione.");
        Check(products.All(p => p.CreatedAtUtc.Year == 2026), "Zmienna data seeda.");
    })),
    ("Ponowny dev seed nie tworzy duplikatów", () => WithDatabase(async db =>
    {
        var env = new FakeEnvironment("Development");
        await DevelopmentDataSeeder.SeedAsync(db, env);
        await DevelopmentDataSeeder.SeedAsync(db, env);
        Check(await db.Products.CountAsync() == 3, "Wystąpiły duplikaty.");
    })),
    ("Seed uzupełnia tylko brakujące SKU", () => WithDatabase(async db =>
    {
        db.Products.Add(new Product("DEMO-001", "Nie nadpisuj mnie", 5m));
        await db.SaveChangesAsync();
        await DevelopmentDataSeeder.SeedAsync(db, new FakeEnvironment("Development"));
        Check(await db.Products.CountAsync() == 3, "Seed nie uzupełnił brakujących danych.");
        Check((await db.Products.SingleAsync(p => p.Sku == "DEMO-001")).Price == 5m,
            "Seed nadpisał istniejący produkt.");
    })),
    ("Seed w Production jest zablokowany", () => WithDatabase(async db =>
    {
        await ExpectThrowsAsync<InvalidOperationException>(
            () => DevelopmentDataSeeder.SeedAsync(db, new FakeEnvironment("Production")));
        Check(await db.Products.CountAsync() == 0, "Production nie może dostać danych demo.");
    })),
    ("appsettings.Production.json nie zawiera connection stringa ani haseł", () =>
    {
        using var json = ReadConfig("appsettings.Production.json");
        var root = json.RootElement;
        Check(!root.TryGetProperty("ConnectionStrings", out _),
            "Nie zapisuj connection stringów produkcyjnych w repo.");
        var raw = root.GetRawText();
        Check(!raw.Contains("Password", StringComparison.OrdinalIgnoreCase) &&
              !raw.Contains("Secret", StringComparison.OrdinalIgnoreCase),
              "Produkcja zawiera podejrzaną wartość.");
        Check(!root.GetProperty("Database").GetProperty("SeedDemoData").GetBoolean(),
            "W Production seed musi być wyłączony.");
        Check(!root.GetProperty("Database").GetProperty("ApplyMigrationsOnStartup").GetBoolean(),
            "W Production automatyczne migracje muszą być wyłączone.");
        return Task.CompletedTask;
    }),
    ("appsettings.Development.json włącza migracje i seed", () =>
    {
        using var json = ReadConfig("appsettings.Development.json");
        var section = json.RootElement.GetProperty("Database");
        Check(section.GetProperty("ApplyMigrationsOnStartup").GetBoolean(), "Dev migracje OFF.");
        Check(section.GetProperty("SeedDemoData").GetBoolean(), "Dev seed OFF.");
        return Task.CompletedTask;
    }),
};

var passed = 0;
foreach (var (name, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"[OK] {name}");
        passed++;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[FAIL] {name}: {ex}");
    }
}

Console.WriteLine($"Wynik: {passed}/{tests.Length} testów EF Core.");
return passed == tests.Length ? 0 : 1;

static async Task WithDatabase(Func<ProductsDbContext, Task> action, bool migrate = true)
{
    var path = Path.Combine(Path.GetTempPath(), $"wwsi-week04-{Guid.NewGuid():N}.db");
    var options = new DbContextOptionsBuilder<ProductsDbContext>()
        .UseSqlite($"Data Source={path}").Options;

    try
    {
        await using var db = new ProductsDbContext(options);
        if (migrate)
            await db.Database.MigrateAsync();
        await action(db);
    }
    finally
    {
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            try { File.Delete(path + suffix); }
            catch (IOException) { /* diagnostyka po zamknięciu procesu */ }
        }
    }
}

static JsonDocument ReadConfig(string fileName) =>
    JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", fileName)));

static void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

static void ExpectThrows<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Oczekiwano wyjątku {typeof(T).Name}.");
}

static async Task ExpectThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Oczekiwano wyjątku {typeof(T).Name}.");
}

sealed class FakeEnvironment(string name) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "Week04.IntegrationTests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

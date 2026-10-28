#!/usr/bin/env python3
"""Statyczne sprawdzenie dostarczonych plików (nie zastępuje dotnet build/test)."""
from pathlib import Path
from xml.etree import ElementTree as ET
import json
import re
import sys

root = Path(__file__).resolve().parent
api = root / "src/Week04.Api"
checks = []


def check(name, predicate):
    ok = bool(predicate)
    checks.append((name, ok))
    print(f"[{'OK' if ok else 'FAIL'}] {name}")


def content(rel):
    file = root / rel
    return file.read_text(encoding="utf-8") if file.is_file() else ""


required_files = [
    "README.md", "TEST_REPORT.md", ".config/dotnet-tools.json",
    "src/Week04.Api/Week04.Api.csproj", "src/Week04.Api/Program.cs",
    "src/Week04.Api/Domain/Product.cs",
    "src/Week04.Api/Data/ProductsDbContext.cs",
    "src/Week04.Api/Data/Configurations/ProductConfiguration.cs",
    "src/Week04.Api/Data/ProductsDbContextFactory.cs",
    "src/Week04.Api/Data/Migrations/202610250001_InitialCreate.cs",
    "src/Week04.Api/Data/Migrations/202610250001_InitialCreate.Designer.cs",
    "src/Week04.Api/Data/Migrations/ProductsDbContextModelSnapshot.cs",
    "src/Week04.Api/Seeding/DevelopmentDataSeeder.cs",
    "src/Week04.Api/appsettings.json",
    "src/Week04.Api/appsettings.Development.json",
    "src/Week04.Api/appsettings.Production.json",
    "src/Week04.Api/Properties/launchSettings.json",
    "src/Week04.Api/Week04.Api.http",
    "tests/Week04.IntegrationTests/Program.cs",
    "tests/Week04.IntegrationTests/Week04.IntegrationTests.csproj",
    "test.sh", "test.ps1",
]
for relative in required_files:
    check(f"Plik {relative}", (root / relative).is_file())

json_files = list(api.rglob("*.json")) + [root / ".config/dotnet-tools.json"]
for file in json_files:
    try:
        json.loads(file.read_text(encoding="utf-8"))
        check(f"JSON poprawny: {file.name}", True)
    except (ValueError, OSError):
        check(f"JSON poprawny: {file.name}", False)

for project in list(root.rglob("*.csproj")):
    try:
        ET.parse(project)
        check(f"XML csproj: {project.name}", True)
    except ET.ParseError:
        check(f"XML csproj: {project.name}", False)

csproj = content("src/Week04.Api/Week04.Api.csproj")
prog = content("src/Week04.Api/Program.cs")
product = content("src/Week04.Api/Domain/Product.cs")
context = content("src/Week04.Api/Data/ProductsDbContext.cs")
config = content("src/Week04.Api/Data/Configurations/ProductConfiguration.cs")
factory = content("src/Week04.Api/Data/ProductsDbContextFactory.cs")
migration = content("src/Week04.Api/Data/Migrations/202610250001_InitialCreate.cs")
designer = content("src/Week04.Api/Data/Migrations/202610250001_InitialCreate.Designer.cs")
snapshot = content("src/Week04.Api/Data/Migrations/ProductsDbContextModelSnapshot.cs")
seed = content("src/Week04.Api/Seeding/DevelopmentDataSeeder.cs")
tests = content("tests/Week04.IntegrationTests/Program.cs")

check(".NET 10 target", "<TargetFramework>net10.0</TargetFramework>" in csproj)
check("EF Core Sqlite package 10.0.0", 'Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0"' in csproj)
check("EF Core Design package 10.0.0", 'Microsoft.EntityFrameworkCore.Design" Version="10.0.0"' in csproj)
check("DB context występuje", "class ProductsDbContext" in context)
check("DbSet<Product> występuje", "DbSet<Product> Products" in context)
check("Konfiguracja DI", "AddDbContext<ProductsDbContext>" in prog)
check("UseSqlite w runtime", "UseSqlite(connectionString)" in prog)
check("Brak wartości połączenia zaszytej w Program.cs", "Data Source=" not in prog)
check("Factory do design-time", "IDesignTimeDbContextFactory<ProductsDbContext>" in factory)
check("Fabryka korzysta z User Secrets", ".AddUserSecrets<" in factory)
check("Fabryka korzysta ze zmiennych środowiskowych", ".AddEnvironmentVariables()" in factory)
check("Encja Product", "class Product" in product)
check("Klucz encji", ".HasKey(" in config)
check("SKU required", "product.Sku)" in config and ".IsRequired()" in config)
check("SKU length", "HasMaxLength(32)" in config)
check("Name length", "HasMaxLength(120)" in config)
check("Description length", "HasMaxLength(500)" in config)
check("Price precision", "HasPrecision(18, 2)" in config)
check("SKU unique", "HasIndex(product => product.Sku).IsUnique()" in config)
check("OnModelCreating", "ApplyConfigurationsFromAssembly" in context)
check("Migracja InitialCreate", 'Migration("202610250001_InitialCreate")' in migration)
check("Migracja Up", "void Up(MigrationBuilder" in migration)
check("Migracja Down", "void Down(MigrationBuilder" in migration)
check("Migracja tworzy Products", 'name: "Products"' in migration and "CreateTable(" in migration)
check("Migracja tworzy indeks", 'name: "IX_Products_Sku"' in migration and "unique: true" in migration)
check("Migracja ma model docelowy", "override void BuildTargetModel" in designer)
check("Snapshot modelu", "class ProductsDbContextModelSnapshot" in snapshot)
check("Snapshot mapuje precyzję", "HasPrecision(18, 2)" in snapshot)
check("MigrateAsync w starcie dev", "app.Environment.IsDevelopment()" in prog and "db.Database.MigrateAsync()" in prog)
check("Seed tylko w dev", "environment.IsDevelopment()" in seed)
check("Seed posiada z góry ustaloną datę", "DemoTimestamp" in seed and "DateTimeKind.Utc" in seed)
check("Seed trzech produktów", all(x in seed for x in ("DEMO-001", "DEMO-002", "DEMO-003")))
check("Seed nie duplikuje istniejącego SKU", "existingSkus.Add(product.Sku)" in seed)
check("SQLite unique index w migracji", 'unique: true' in migration)
check("Środowiska mają osobne appsettings", (api/"appsettings.Development.json").exists() and (api/"appsettings.Production.json").exists())

try:
    dev = json.loads(content("src/Week04.Api/appsettings.Development.json"))
    prod = json.loads(content("src/Week04.Api/appsettings.Production.json"))
    base_settings = json.loads(content("src/Week04.Api/appsettings.json"))
    check("Development ma connection string", bool(dev.get("ConnectionStrings", {}).get("ProductsDb")))
    check("Development migruje przy starcie", dev["Database"]["ApplyMigrationsOnStartup"] is True)
    check("Development seeduje przy starcie", dev["Database"]["SeedDemoData"] is True)
    check("Production bez connection stringa w pliku", "ConnectionStrings" not in prod)
    check("Production bez automatycznej migracji", prod["Database"]["ApplyMigrationsOnStartup"] is False)
    check("Production bez seed", prod["Database"]["SeedDemoData"] is False)
    check("Bazowa konfiguracja bez danych połączenia", "ConnectionStrings" not in base_settings)
    check("Brak plaintext hasła w produkcyjnym JSON", not re.search(r'password|pwd|token|secret', json.dumps(prod), re.I))
except (ValueError, KeyError) as error:
    check(f"Parsowanie appsettings: {error}", False)

check("Kontrakt HTTP produktów", 'MapGroup("/api/products")' in prog)
check("HTTP GET produktów", 'products.MapGet("/"' in prog)
check("HTTP GET /:id", 'products.MapGet("/{id:int}"' in prog)
check("Testy EF Core rzeczywiście migrują bazę", "db.Database.MigrateAsync()" in tests)
check("Testy EF Core kontrolują duplikaty", "ExpectThrowsAsync<DbUpdateException>" in tests)
check("Testy EF Core kontrolują środowiska", 'FakeEnvironment("Production")' in tests)
check("Testy EF Core kontrolują seed", "DevelopmentDataSeeder.SeedAsync" in tests)
check("Testy EF Core kontrolują produkcję", 'ReadConfig("appsettings.Production.json")' in tests)
check("Min. 20 scenariuszy integracyjnych", len(re.findall(r'^    \("', tests, re.M)) >= 20)

workflow = root.parent / ".github/workflows/ci.yml"
check("GitHub Actions", workflow.exists())
if workflow.exists():
    wf = workflow.read_text(encoding="utf-8")
    check("GitHub Actions uruchamia .NET 10", "dotnet-version: '10.0.x'" in wf)
    check("GitHub Actions testuje SQLite", "bash \"week${{ matrix.week }}/test.sh\"" in wf and "Week04.IntegrationTests" in content("test.sh"))
    check("GitHub Actions testuje HTTP", "http://127.0.0.1:$port/api/products" in content("test.sh"))

passed = sum(ok for _, ok in checks)
print(f"\nKontrola statyczna: {passed}/{len(checks)}")
sys.exit(0 if passed == len(checks) else 1)

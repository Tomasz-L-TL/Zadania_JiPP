# Week 04 — Entity Framework Core: DbContext i migracje (100 pkt)

Pełne rozwiązania ćwiczeń [WWSI, semestr 5, tydzień 4](https://github.com/marcinpytlik/WWSI_CSharp/blob/main/semestr5/exercises/week04/README.md). Projekt przygotowany w **C# / .NET 10 / EF Core 10 / SQLite**.

## Mapa zadań

| Zadanie | Pkt | Implementacja |
|---|---:|---|
| 1. Pierwszy DbContext | 10 | `Data/ProductsDbContext.cs`, `Domain/Product.cs`, DI w `Program.cs`, `appsettings.*.json` |
| 2. Fluent API | 15 | `Data/Configurations/ProductConfiguration.cs`: PK, wymagane SKU/Name, długości, precyzja ceny, unikalność SKU |
| 3. Migracja | 20 | `Data/Migrations/202610250001_InitialCreate.cs`, snapshot modelu oraz opis CLI poniżej |
| 4. Seed danych | 25 | `Seeding/DevelopmentDataSeeder.cs`, 3 stałe produkty, ochrona przed duplikatami i Production |
| 5. Środowiska | 30 | osobne `appsettings.Development.json` / `appsettings.Production.json`, User Secrets, zmienne środowiskowe, `.gitignore` |

## Wymagania

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Dostęp do NuGet przy pierwszym restore (`Microsoft.EntityFrameworkCore.Sqlite` i `Microsoft.EntityFrameworkCore.Design` w wersji 10.0.0)
- `dotnet-ef` 10.0.0 — lokalny tool manifest `week04/.config/dotnet-tools.json`
- Opcjonalnie Python 3 do kontroli statycznej i skryptu smoke na Linux/macOS.

## Start w Development

W katalogu głównym repozytorium:

```bash
dotnet restore week04/src/Week04.Api/Week04.Api.csproj
dotnet run --project week04/src/Week04.Api/Week04.Api.csproj -- --urls http://localhost:5084
```

Przy uruchomieniu przez profil `dotnet run` plik `launchSettings.json` ustawia `ASPNETCORE_ENVIRONMENT=Development`. Jeśli używasz `--no-launch-profile`, ustaw tę zmienną samodzielnie. Powstanie lokalny plik SQLite (`week04-dev.db` w katalogu bieżącym), wykona się migracja, a tabela Products otrzyma 3 rekordy. Ponowne uruchomienie aplikacji **nie tworzy dodatkowych rekordów**.

Sprawdź:

```bash
curl http://localhost:5084/health
curl http://localhost:5084/api/products
curl http://localhost:5084/api/products/1
```

Możesz też otworzyć `src/Week04.Api/Week04.Api.http` w Visual Studio lub VS Code z klientem HTTP.

## CLI migracji: add / update / remove

Z katalogu głównego repozytorium wykonaj:

```bash
cd week04
dotnet tool restore

# Zobacz migrację dołączoną do rozwiązania:
dotnet ef migrations list --project src/Week04.Api --startup-project src/Week04.Api

# Zastosuj istniejącą migrację do bazy:
dotnet ef database update --project src/Week04.Api --startup-project src/Week04.Api

# Tworzenie KOLEJNEJ migracji (InitialCreate już istnieje):
dotnet ef migrations add AddProductCategory --project src/Week04.Api --startup-project src/Week04.Api

# Wycofanie OSTATNIEJ migracji z kodu (najpierw cofnij ją w bazie):
dotnet ef migrations remove --project src/Week04.Api --startup-project src/Week04.Api
```

Aby bezpiecznie wycofać migrację z bazy, użyj `dotnet ef database update <PoprzedniaMigracja> ...` (dla początkowej migracji: `0`). **Uwaga: cofnięcie pierwszej migracji usuwa tabelę i jej dane.** Zrób backup przed takim krokiem. `dotnet ef migrations remove` bez wycofania ostatniej zastosowanej migracji zwykle odmówi usunięcia, chyba że użyjesz flagi wymuszającej, której tutaj celowo nie zalecamy.

### Sekrety w Development

Z katalogu głównego repozytorium:

```bash
# Ustawienie prywatnego connection stringa (poza Gitem):
dotnet user-secrets set "ConnectionStrings:ProductsDb" "Data Source=local-personal.db" --project week04/src/Week04.Api

# Przeglądanie zapisanych User Secrets:
dotnet user-secrets list --project week04/src/Week04.Api

# Usunięcie nadpisania — powrót do appsettings.Development.json:
dotnet user-secrets remove "ConnectionStrings:ProductsDb" --project week04/src/Week04.Api
```

`User Secrets` **nie jest sejfem produkcyjnym** — służy do lokalnej pracy. Nie commituj plików SQLite, tokenów, haseł ani `.env`.

### Produkcja

`appsettings.Production.json` **nie zawiera połączenia do bazy**. Produkcja musi dostać wartość poprzez `ConnectionStrings__ProductsDb` ze środowiska / sekretu wdrożeniowego. Przykład Linux/macOS:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__ProductsDb='Data Source=/var/lib/week04/products.db'

# Przy wdrożeniu i po wykonaniu kopii zapasowej:
cd week04
dotnet tool restore
dotnet ef database update --project src/Week04.Api --startup-project src/Week04.Api

# Uruchom aplikację, ale BEZ migracji i seeda podczas startu:
dotnet run --project src/Week04.Api --no-launch-profile -- --urls http://127.0.0.1:5084
```

Ścieżka produkcyjna to przykład, katalog musi istnieć i mieć odpowiednie uprawnienia. W realnym wdrożeniu umieść connection string w menedżerze sekretów lub chronionej konfiguracji CI/CD. Ogranicz uprawnienia do pliku bazy. Zwykła zmienna środowiskowa nie jest automatycznie tajna — zależy od sposobu przechowywania.

**Ważne:** nie wdrażaj pliku z `appsettings.Development.json` jako konfiguracji Production. Kod dodatkowo sprawdza `app.Environment.IsDevelopment()` przed migracjami automatycznymi i seedem, a sam seeder odrzuca inne środowiska.

## Testy

```bash
# Linux/macOS (z repo root)
bash week04/test.sh

# Windows (PowerShell, z repo root)
.\week04\test.ps1

# Tylko testy EF (bez smoke HTTP):
dotnet run --project week04/tests/Week04.IntegrationTests/Week04.IntegrationTests.csproj

# Kontrole statyczne:
python3 week04/static_check.py
```

Testy integracyjne używają niezależnych fizycznych plików SQLite w folderze tymczasowym, stosują migrację EF Core, testują ograniczenia modelu, indeks SKU, dane startowe, konfiguracje środowisk i blokadę seeda w Production. Smoke HTTP sprawdza automatyczny start migracji + 3 rekordy w `GET /api/products`. CI: `.github/workflows/week04-ci.yml`.

## Uwagi techniczne

- SQLite zapisuje `decimal` jako `TEXT`. Reguła `.HasPrecision(18, 2)` jest uwzględniona w **modelu EF**, ale SQLite nie egzekwuje jej w taki sposób jak SQL Server/PostgreSQL. Konstruktor encji zaokrągla cenę do 2 miejsc. Provider serwerowy byłby zalecany przy ścisłych gwarancjach precyzji i większym ruchu.
- SQLite nie wymusza długości `varchar` na poziomie DB; `HasMaxLength` jest metadanymi mapowania. Walidacja wejścia jest odrębnym tematem i warto ją dodać w kolejnym tygodniu.
- `POST/PUT/DELETE` produktów zostaną rozszerzone w **Week 05 (CRUD z EF Core)**, dlatego tutaj jest tylko `GET` pomocniczy do prezentacji działania bazy i seeda.
- `ApplyMigrationsOnStartup` oraz `SeedDemoData` zadziałają jedynie, gdy aktywne jest środowisko Development. W Production migracje wykonuj oddzielnym krokiem wdrożenia.

## Stan weryfikacji

Zobacz `TEST_REPORT.md`. Kontrola statyczna i sprawdzenie plików mogą być wykonane bez SDK, ale **nie zastępują kompilacji .NET ani wykonania testów EF Core**. Wyniki CI po wrzuceniu do GitHuba rozstrzygają testy uruchomieniowe.

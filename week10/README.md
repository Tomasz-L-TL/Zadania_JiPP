# WWSI — Semestr 5 — Week 10: Projekt końcowy Users + Tasks API

**5/5 ćwiczeń, 100 możliwych punktów według treści zadań**, ASP.NET Core Minimal API **.NET 10**, EF Core 10 i SQLite. Źródło wymagań: https://github.com/marcinpytlik/WWSI_CSharp/blob/main/semestr5/exercises/week10/README.md.

Projekt można uruchomić samodzielnie — **nie wymaga** folderów week01–week09. Wszystkie endpointy są asynchroniczne przy operacjach na bazie. Hasła są hashowane przez `PasswordHasher<User>`, a tokeny JWT mają ważność 30 minut.

## Pokrycie zadań

| Zadanie | Pkt | Główne pliki / realizacja |
|---|---:|---|
| 1. Model domeny | 10 | `Domain/Entities.cs`, `Contracts/Contracts.cs`, `Data/AppDbContext.cs`, `Data/Migrations/*` — User 1:N Tasks i Task ↔ Tag przez TaskTag, indeksy i FK |
| 2. CRUD | 15 | `Endpoints/UserEndpoints.cs`, `TaskEndpoints.cs` i `AuthEndpoints.cs` — create/read/update/delete, async, DTO, 400/404, sortowanie, filtrowanie i paginacja |
| 3. Security | 20 | `Security/JwtService.cs`, `AuthEndpoints.cs`, owner w Tasks, polityka `AdminOnly`, JWT i 401/403 |
| 4. Raporty | 25 | `ReportEndpoints.cs` — po statusie, po użytkowniku, popularność tagów, zaległe zadania; translowalne zapytania LINQ/EF |
| 5. Projekt końcowy | 30 | migracja, bezpieczny developer seed, obsługa wyjątków, structured logging, **49 testów integracyjnych xUnit**, workflow i niniejszy opis |

> 100 pkt to punktacja **możliwa do zdobycia**, nie oficjalna ocena. Wyniki testów uruchomieniowych należy potwierdzić po instalacji .NET 10 — szczegóły w `TEST_REPORT.md`.

## Struktura

```text
week10/
  Week10.slnx
  .config/dotnet-tools.json
  src/Week10.Api/
    Domain/Entities.cs
    Contracts/Contracts.cs
    Data/AppDbContext.cs
    Data/AppDbContextFactory.cs
    Data/Migrations/202610260001_InitialCreate.cs
    Data/Migrations/202610260001_InitialCreate.Designer.cs
    Data/Migrations/AppDbContextModelSnapshot.cs
    Security/JwtService.cs
    Services/{ApiHelpers,DevSeeder}.cs
    Endpoints/{Auth,User,Task,Tag,Report}Endpoints.cs
    appsettings*.json
    Week10.Api.http
  tests/Week10.IntegrationTests/
    ApiFactory.cs
    ApiTests.cs
    Week10.IntegrationTests.csproj
  test.sh, test.ps1, static_check.py, TEST_REPORT.md
.github/workflows/week10-ci.yml
```

## Wymagania i uruchomienie w Development

Wymagania: SDK [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) oraz dostęp do NuGet przy pierwszym przywróceniu pakietów. Z katalogu głównego repozytorium:

```bash
# Windows PowerShell
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:Jwt__SigningKey="ReplaceThisWithYourOwnSecretKeyThatIsAtLeast32Bytes_2026"
dotnet run --project week10/src/Week10.Api/Week10.Api.csproj -- --urls=http://localhost:5010
```

Linux/macOS:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export Jwt__SigningKey='ReplaceThisWithYourOwnSecretKeyThatIsAtLeast32Bytes_2026'
dotnet run --project week10/src/Week10.Api/Week10.Api.csproj -- --urls=http://localhost:5010
```

Przykładowy klucz powyżej jest wyłącznie do **lokalnego uruchomienia**; nie wolno go używać w Production. Lepszą praktyką jest wygenerowanie własnego silnego sekretu i podanie go przez menedżer sekretów / `dotnet user-secrets`.

W Development program automatycznie tworzy `week10-dev.db`, stosuje migrację i dodaje cztery publiczne tagi demo. Nie tworzy żadnych użytkowników z zapisanym w repozytorium hasłem.

### Konto administratora do testów lokalnych

W Development, **przed uruchomieniem** ustaw trzy dodatkowe wartości (unikalne, własne dane):

```bash
# Linux/macOS — zastąp bezpiecznymi własnymi wartościami:
export DevelopmentSeed__AdminUsername='administrator'
export DevelopmentSeed__AdminEmail='administrator@example.test'
export DevelopmentSeed__AdminPassword='UseYourOwnStrongPassword_AtLeast12Characters'
```

Odpowiedniki Windows to `$env:DevelopmentSeed__AdminUsername=...`, itd. Przy pierwszym uruchomieniu seed utworzy administratora (`Role=Admin`) z hasłem zahashowanym przez `PasswordHasher`; kolejne uruchomienia nie tworzą duplikatów. **W Production seed jest zablokowany**. Jeżeli nie podasz żadnej z trzech wartości, aplikacja nadal działa, ale nie tworzy admina. Jeśli podasz tylko część — uruchomienie zgłasza błąd konfiguracji zamiast zakładać słabe dane.

### User Secrets (alternatywa do zmiennych środowiskowych)

```bash
dotnet user-secrets set "Jwt:SigningKey" "YOUR_RANDOM_PRIVATE_SECRET_32PLUS_BYTES" --project week10/src/Week10.Api
```

Nie commituj sekretów, pliku `.env` ani bazy. API uruchamiane bez klucza JWT **nie wystartuje** — to zamierzony bezpieczny warunek.

## Endpointy

| Metoda | Endpoint | Uprawnienia | Funkcja |
|---|---|---|---|
| GET | `/health` | publiczny | Status aplikacji |
| POST | `/auth/register` i `/users` | publiczny | Rejestracja użytkownika (201 / 400 / 409) |
| POST | `/auth/login` | publiczny | JWT (200 / 401) |
| GET | `/users` | Admin | Filtrowana lista użytkowników z paginacją |
| GET | `/users/{id}` | właściciel / Admin | Dane użytkownika |
| PUT | `/users/{id}` | właściciel / Admin | Zmiana e-maila |
| DELETE | `/users/{id}` | właściciel / Admin | Usunięcie użytkownika i kaskadowo zadań |
| GET | `/tasks` | JWT | Własne zadania; Admin wszystkie, paginacja i filtry |
| POST | `/tasks` | JWT | Utworzenie zadania z tagami |
| GET | `/tasks/{id}` | właściciel / Admin | Odczyt (cudze zasoby zwracają 404 dla ochrony prywatności) |
| PUT | `/tasks/{id}` | właściciel / Admin | Aktualizacja zadania i tagów (bez zmiany właściciela) |
| DELETE | `/tasks/{id}` | właściciel / Admin | Usunięcie |
| GET | `/tags` | JWT | Lista tagów |
| POST | `/tags` | Admin | Dodanie tagu |
| GET | `/reports/tasks/by-status` | Admin | Liczba zadań w każdym statusie |
| GET | `/reports/tasks/by-user` | Admin | Liczba zadań według użytkownika, również zero |
| GET | `/reports/tags/top?limit=10` | Admin | Najpopularniejsze tagi |
| GET | `/reports/tasks/overdue` | Admin | Zaległe, nieukończone zadania |

### Przykładowy scenariusz curl

```bash
# 1. Rejestracja
curl -i -X POST http://localhost:5010/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"username":"student01","email":"student01@example.test","password":"StrongPassword_123!"}'

# 2. Login — otrzymasz accessToken
curl -i -X POST http://localhost:5010/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"student01","password":"StrongPassword_123!"}'

# 3. Zastąp TOKEN odpowiedzią z logowania
curl -i -X POST http://localhost:5010/tasks \
  -H "Authorization: Bearer TOKEN" -H 'Content-Type: application/json' \
  -d '{"title":"Finish Week 10","description":"Test","status":"Todo","tagIds":[1]}'

# 4. Lista
curl -H "Authorization: Bearer TOKEN" 'http://localhost:5010/tasks?page=1&pageSize=10&status=Todo'
```

Status zadania: `Todo`, `InProgress`, `Done`. `page` 1–10000, `pageSize` 1–50; błędy walidacji zwracają 400 JSON ProblemDetails. Użytkownik nie może ustawić sobie roli `Admin`; API nie przyjmuje roli w rejestracji ani w edycji konta. Raporty wymagają polityki `AdminOnly`.

## Migracje EF Core

W projekcie jest `InitialCreate` wraz z docelowym modelem migracji i snapshotem. W Development migracja jest automatycznie nakładana na nową SQLite. Ręczne polecenia (po ustawieniu `ASPNETCORE_ENVIRONMENT` i połączenia do DB):

```bash
cd week10
dotnet tool restore
dotnet ef migrations list --project src/Week10.Api --startup-project src/Week10.Api
dotnet ef database update --project src/Week10.Api --startup-project src/Week10.Api
# Przykład dodania kolejnej migracji po zmianie domeny:
dotnet ef migrations add AddField --project src/Week10.Api --startup-project src/Week10.Api
```

W **Production** przekazuj `ConnectionStrings__AppDb` i `Jwt__SigningKey` z menedżera sekretów. Produkcja ma wyłączone `ApplyMigrationsOnStartup` i `SeedDemoData`; migrację wykonuj świadomie w ramach wdrożenia, z kopią zapasową bazy. Plik `appsettings.Production.json` nie zawiera żadnych poświadczeń.

## Architektura i optymalizacja

- `Domain` to encje EF i typ statusu; `Contracts` zawiera DTO wejściowe, wyjściowe i raportowe, bez pól `PasswordHash`.
- `Data` mapuje klucze, indeksy, relacje 1:N i N:M, kaskady oraz status enum jako tekst; migracja dostarcza schemat SQL.
- `Endpoints` separuje auth, CRUD, tagi i raportowanie; `Services/ApiHelpers` agreguje walidację i operacje wspólne.
- JWT jest weryfikowany pod kątem podpisu, emitenta, odbiorcy i terminu; zwykły użytkownik zarządza tylko własnymi zadaniami, Admin — wszystkimi.
- Listy używają `AsNoTracking()`, liczników `CountAsync`, filtrów w SQL i `Skip/Take`; tagi są ładowane dla strony wyników przez `Include(...).ThenInclude(...)` z `AsSplitQuery()`.
- Raporty używają projekcji LINQ w SQL bez pobierania wszystkich encji do pamięci; status enum jest przekonwertowany na string **po** agregacji SQL.
- Middleware `UseExceptionHandler()` z `AddProblemDetails()` mapuje nieobsłużone wyjątki na ProblemDetails; logger używa szablonów strukturalnych (`{Method}`, `{Path}`, `{StatusCode}`, `{ElapsedMs}`) bez logowania payloadów i tokenów.

## Testowanie

```bash
# Linux/macOS (z katalogu głównego repozytorium)
./week10/test.sh

# Windows PowerShell
./week10/test.ps1

# ręcznie
dotnet test week10/tests/Week10.IntegrationTests/Week10.IntegrationTests.csproj -c Release
```

Testy xUnit korzystają z `WebApplicationFactory<Program>` i **osobnej bazy SQLite w pamięci na instancję fabryki**, z prawdziwą migracją oraz adminem skonfigurowanym wyłącznie w kodzie testów. Pokrywają wszystkie zadania: rejestrację i logowanie, 400/401/403/404, odczyt i modyfikację użytkowników/zadań, paginację, uprawnienia właściciela i administratora, cztery raporty, powiązania tagów, migrację i izolację danych.

CI w `.github/workflows/week10-ci.yml` automatycznie uruchamia `dotnet restore`, `dotnet build` i `dotnet test`. W środowisku generowania paczki brak .NET 10 SDK, dlatego testy nie zostały w nim wykonane; nie należy interpretować dołączonych plików testowych jako zaliczonego przebiegu. Pełny raport: `TEST_REPORT.md`.

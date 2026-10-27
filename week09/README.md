# WWSI — semestr 5 — Week 09: Testy integracyjne (100 pkt)

Komplet rozwiązania na podstawie [oryginalnych zadań Week 09](https://github.com/marcinpytlik/WWSI_CSharp/blob/main/semestr5/exercises/week09/README.md). Projekt samodzielny — nie wymaga Week 01–08. Wykorzystuje **.NET 10, ASP.NET Core Minimal API, EF Core 10, SQLite-in-memory, WebApplicationFactory oraz xUnit**.

## Mapa wymagań

| Zadanie | Punkty | Pliki i pokrycie |
|---|---:|---|
| Pierwszy test API | 10 | `IntegrationTests.Health_returns_200_and_expected_JSON_body`, `TestApiFactory`: `WebApplicationFactory<Program>`, `HttpClient`, status 200 i body |
| Test 404 | 15 | `Missing_note_returns_404_independent_of_data`, `Unknown_endpoint_returns_404_with_correlation_id` i osobna fabryka dla każdego testu |
| Test POST | 20 | `Successful_POST_creates_a_note_with_201_and_Location`, `Invalid_POST_returns_400_with_validation_errors`, DataAnnotations i JSON |
| Test z bazą testową | 25 | `TestApiFactory`: podmiana `DbContextOptions` przez `ConfigureTestServices`, fizycznie otwarte połączenie `SqliteConnection("Data Source=:memory:")` na instancję, `EnsureCreatedAsync`, `Dispose` |
| Pakiet integracyjny | 30 | 39 testów `[Fact]`: CRUD + rejestracja/login JWT + role i autoryzacja + walidacja + middleware + brak dostępu do cudzych zasobów |

**100 pkt to suma punktów z treści zadań, nie oficjalna ocena od prowadzącego.**

## Struktura

```text
week09/
├── src/Week09.Api/
│   ├── Program.cs
│   ├── Domain/Entities.cs
│   ├── Data/Week09DbContext.cs
│   ├── Contracts/ApiDtos.cs
│   ├── Contracts/RequestValidation.cs
│   ├── Endpoints/AuthEndpoints.cs
│   ├── Endpoints/NoteEndpoints.cs
│   ├── Middleware/CorrelationIdMiddleware.cs
│   ├── Security/JwtTokenService.cs
│   ├── appsettings*.json
│   └── Week09.Api.http
├── tests/Week09.IntegrationTests/
│   ├── TestApiFactory.cs
│   ├── ApiScenario.cs
│   ├── IntegrationTests.cs (39 testów xUnit)
│   └── Week09.IntegrationTests.csproj
├── tools/static_check.py
├── test.sh
├── test.ps1
├── TEST_REPORT.md
└── README.md
```

## Wymagania i uruchomienie

Zainstaluj [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Przy pierwszym `dotnet restore` potrzebny jest dostęp do NuGet. Archiwum **nie** zawiera gotowych zbudowanych binarek ani katalogów `bin/obj/`.

**Linux/macOS (w katalogu głównym repo):**

```bash
bash week09/test.sh
```

**Windows PowerShell:**

```powershell
.\week09\test.ps1
```

**Ręcznie:**

```bash
dotnet restore week09/tests/Week09.IntegrationTests/Week09.IntegrationTests.csproj
dotnet test week09/tests/Week09.IntegrationTests/Week09.IntegrationTests.csproj -c Release --logger 'console;verbosity=normal'
```

Testy uruchamiają serwer **w pamięci procesu** przez `WebApplicationFactory`, nie przez zewnętrzny port TCP. Każdy scenariusz (`new ApiScenario()`) buduje osobny `TestApiFactory`, z nowym połączeniem do SQLite-in-memory. Po jego zamknięciu testowa baza jest niszczona. To eliminuje zależności od kolejności testów, pozwala na równoległe wykonanie i nie dotyka produkcyjnej bazy. Fabryka nadpisuje testowym kluczem JWT wyłącznie konfigurację lokalnego hosta testowego. Dane rejestracji i notatek nie wychodzą poza testową bazę.

## API dostępne w projekcie

| Metoda | Trasa | Autoryzacja | Odpowiedź |
|---|---|---|---|
| GET | `/health` | publiczna | 200 + JSON `{ "status": "healthy" }` |
| POST | `/auth/register` | publiczna | 201, 400 (walidacja), 409 (duplikat email) |
| POST | `/auth/login` | publiczna | 200 + JWT, 400, 401 |
| GET | `/auth/me` | Bearer JWT | 200, 401 |
| GET | `/api/notes` | Bearer JWT | 200, lista **własnych** notatek |
| GET | `/api/notes/{id}` | Bearer JWT | 200, 404 |
| POST | `/api/notes` | Bearer JWT | 201 + `Location`, 400 |
| PUT | `/api/notes/{id}` | Bearer JWT | 200, 400, 404 |
| DELETE | `/api/notes/{id}` | Bearer JWT | 204, 404 |
| GET | `/api/admin/stats` | rola `Admin` | 200, 401, 403 |

Endpoint `/__test/throw` istnieje **wyłącznie** w środowisku `Testing` i pozwala sprawdzić middleware wyjątków; nie jest mapowany w Development ani Production. Wszystkie odpowiedzi otrzymują `X-Correlation-ID` (niewłaściwy nagłówek jest zastępowany nowym identyfikatorem). Handler wyjątków zwraca `ProblemDetails` 500 bez szczegółów wyjątku.

### Jak uruchomić API lokalnie

Aplikacja wymaga **osobnego sekretu JWT** z co najmniej 32 bajtami entropii/kodu UTF-8. Nie umieszczaj go w repozytorium ani w commitach. Przykładowo w Bash:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export Jwt__SigningKey="$(openssl rand -base64 48)"
dotnet run --project week09/src/Week09.Api/Week09.Api.csproj -- --urls=http://localhost:5089
```

W PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet run --project week09/src/Week09.Api/Week09.Api.csproj -- --urls=http://localhost:5089
```

Development używa lokalnego `week09-dev.db`, automatycznie inicjowanego schematem EF Core (`EnsureCreatedAsync`). Taki skrót jest wystarczający dla ćwiczenia poświęconego testom — **nie jest strategią migracji produkcyjnych**. W Production wymagaj connection stringa przez `ConnectionStrings__Default` z menedżera sekretów oraz wdrożonego, uprzednio utworzonego schematu bazy. Żaden sekret nie jest wpisany w `appsettings.json` ani `appsettings.Production.json`.

Do ręcznych prób służy `src/Week09.Api/Week09.Api.http` (Visual Studio, VS Code REST Client). Przed wywołaniem chronionych tras zarejestruj konto, zaloguj się i skopiuj `accessToken` do `@token`.

## 39 testów integracyjnych: zakres

Testy sprawdzają m.in.:

- `WebApplicationFactory` i `/health` (200 + body), korelację dla sukcesu i błędu 404;
- brak notatki (404), brak uprawnień (401), brak roli (403);
- poprawny POST (201 + JSON + `Location`), błędny POST (400 + szczegóły walidacji), długą treść;
- przechowywanie notatek i użytkowników w SQLite, trwałość danych pomiędzy dwoma `HttpClient` jednej fabryki;
- niezależność fabryk oraz izolację notatek różnych użytkowników;
- pełny CRUD (`GET`, `POST`, `PUT`, `DELETE`) i zachowanie danych po błędzie;
- rejestrację, login JWT, niepoprawne hasło i podpis, brak ujawniania hasha w API;
- kontrolę roli `Admin` oraz awarię z odpowiedzią 500 bez ujawniania wyjątków w treści.

Testy tworzą role administratora **wyłącznie we własnej bazie** przez `PromoteToAdminAsync`, a następnie logują się normalnie w celu otrzymania rzeczywistego JWT z rolą. Publiczne API nie pozwala przypisać sobie roli Admin.

## GitHub Actions

Workflow `.github/workflows/week09-ci.yml` po pushu lub pull requeście do `week09/` wykonuje kontrolę statyczną, restore, build, `dotnet test` i zapisuje raport `.trx` jako artefakt. Testy nie wymagają usług zewnętrznych, kont w chmurze ani prawdziwych poświadczeń produkcyjnych.

## Weryfikacja i ograniczenia

Plik `TEST_REPORT.md` odróżnia **rzeczywiście wykonane kontrole statyczne** od **39 testów xUnit przygotowanych, ale niewykonanych** tutaj, ponieważ środowisko tworzenia paczki nie zawiera .NET SDK. Po wrzuceniu plików na GitHuba **sprawdź wynik Actions**; tylko zielony build i testy pozwalają uznać wykonanie testów za potwierdzone.

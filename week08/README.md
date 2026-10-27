# Week 08 — Middleware i logowanie (.NET 10)

Kompletny zestaw rozwiązań 5 ćwiczeń z [WWSI C# — semestr 5, week08](https://github.com/marcinpytlik/WWSI_CSharp/blob/main/semestr5/exercises/week08/README.md). Maksymalna punktacja zadań: **100 pkt**. Nie jest to oficjalnie przyznana ocena.

Aplikacja to samodzielne **ASP.NET Core Minimal API (C# / .NET 10)** bez zewnętrznych pakietów NuGet. Używa `ILogger` z formatowaniem **JSON Console** zamiast dodatkowej biblioteki Serilog, co jest dopuszczone w zadaniu.

## Mapa ćwiczeń

| Ćwiczenie | Pkt | Realizacja |
|---|---:|---|
| 1. Request timing | 10 | `RequestTimingMiddleware` używa `Stopwatch`; jedno zdarzenie `Information` na request zawiera metodę, bezpieczny wzorzec trasy, HTTP status i czas w ms |
| 2. Correlation ID | 15 | `CorrelationIdMiddleware` ustawia `X-Correlation-ID` na każdym żądaniu i odpowiedzi, generuje UUID, gdy brakuje poprawnej wartości, tworzy scope logowania |
| 3. Global exceptions | 20 | `GlobalExceptionMiddleware` obsługuje błędy całej aplikacji i zwraca **500 `application/problem+json`** z `correlationId`, ale bez stack trace i treści wyjątku |
| 4. Structured logging | 25 | `builder.Logging.AddJsonConsole` + parametry nazwane `Method`, `Path`, `StatusCode`, `ElapsedMs`, `UserId`, `CorrelationId`, `ExceptionType` i poziomy Information/Error; wykluczenie danych prywatnych |
| 5. Observability mini-stack | 30 | `/health/live`, `/health/dependencies` z prawdziwym testem zapisu do katalogu, request timing, identyfikator korelacji, spójna centralna obsługa wyjątków oraz poniższa procedura diagnostyczna |

## Uruchomienie

Wymagane: **.NET 10 SDK**.

Z głównego katalogu repozytorium (nad folderem `week08`):

```bash
dotnet run --project week08/src/Week08.Api/Week08.Api.csproj -- --urls=http://localhost:5088
```

Profil `Properties/launchSettings.json` ustawia `ASPNETCORE_ENVIRONMENT=Development`. W przypadku uruchamiania z `--no-launch-profile` ustaw tę zmienną samodzielnie, jeżeli chcesz korzystać z testowego `/api/demo/fail`.

### Endpoints

| Metoda | Adres | Działanie |
|---|---|---|
| GET | `/health/live` | Sprawdza, czy aplikacja odpowiada; 200 |
| GET | `/health/dependencies` | Próbuje faktycznie zapisać plik i go usunąć; 200, gdy storage działa; 503, gdy niedostępny |
| GET | `/api/demo/trace` | 200 + wygenerowany lub przekazany correlation ID w JSON i nagłówku |
| POST | `/api/demo/submit` | Odpowiada `{ "accepted": true }`, nie loguje ani nie zwraca przesłanego pola `note` |
| GET | `/api/demo/fail` | Tylko Development: celowy wyjątek testowy -> 500 i bezpieczny ProblemDetails; w Production 404 |

Przykładowe żądania są w `src/Week08.Api/Week08.Api.http`.

## Correlation ID

Nagłówek: **`X-Correlation-ID`**. Serwer zachowuje przesłany identyfikator, jeżeli zawiera 1–64 znaków z dozwolonego zestawu liter, cyfr, kropek, `_`, `-` i zaczyna się od litery/cyfry. Pusty, zbyt długi lub niebezpieczny nagłówek jest zastępowany nowym losowym UUID w formacie `N`.

```bash
curl -i -H 'X-Correlation-ID: lekcja08-test-123' http://localhost:5088/api/demo/trace
```

Możesz powiązać odpowiedź z logami po `CorrelationId`. Każde żądanie logowane jest raz przez timing middleware — nie zapisujemy nagłówków, treści ani query stringa. Do logu trafia **wzorzec trasy**, np. `/api/demo/trace`, a nie surowy adres zawierający potencjalnie wrażliwe identyfikatory (PII). Dla nierozpoznanych ścieżek `Path` to `[unmatched]`.

`UserId` w logach jest ustawiany jedynie wtedy, gdy uwierzytelniony użytkownik ma identyfikator GUID w `ClaimTypes.NameIdentifier`; w przeciwnym razie jest `null`. Nie pobieramy tożsamości z nagłówka `X-User-ID` ani z e-maila. W tej samodzielnej aplikacji nie ma modułu uwierzytelniania — logika jest gotowa do użycia z JWT z Week 07.

## Bezpieczny globalny handler

W Development:

```bash
curl -i -H 'X-Correlation-ID: debug-08-123' http://localhost:5088/api/demo/fail
```

Zwracany jest HTTP 500 i dokument ProblemDetails z polami `title`, `status`, `detail`, `correlationId` i `traceId`. **Nie** zawiera nazwy pliku, stack trace ani komunikatu z oryginalnego wyjątku. W logu błędu (`Error`) pojawia się jedynie rodzaj wyjątku i `CorrelationId`. Szczegóły wyjątku mogą zawierać sekrety i dlatego nie są logowane przez nasz handler.

## Health check zależności

`/health/dependencies` sprawdza rzeczywistą możliwość utworzenia i zapisania pliku testowego w katalogu `Diagnostics:StorageDirectory` (domyślnie `data` względem katalogu projektu), a następnie jego usunięcia. Plik jest automatycznie usuwany przy zamknięciu strumienia. Odpowiedź zawiera **tylko nazwę zależności i stan**; nie ujawnia ścieżek, komunikatów wyjątków ani uprawnień systemowych.

Możesz wskazać inny katalog zmienną środowiskową `Diagnostics__StorageDirectory`:

```bash
# Linux / macOS
export Diagnostics__StorageDirectory=/tmp/week08-storage
```

Katalog musi być zapisywalny dla procesu. Przy błędzie testu endpoint zwraca 503 (`Unhealthy`), podczas gdy `/health/live` nadal zwraca 200. Nie należy umieszczać sekretów ani danych osobowych w katalogu danych diagnostycznych.

## Jak zdiagnozować żądanie od wejścia do wyjątku

1. **Wyślij żądanie** w Development z identyfikatorem `X-Correlation-ID: debug-08-123` na `/api/demo/fail`.
2. **Odczytaj 500 ProblemDetails** z odpowiedzi i potwierdź, że nagłówek `X-Correlation-ID` i pole `correlationId` mają wartość `debug-08-123`. To wspólny klucz wyszukiwania zdarzeń.
3. **W logach JSON znajdź zdarzenia z `CorrelationId` równym `debug-08-123`**: jedno z `Category` kończącym się na `GlobalExceptionMiddleware` (poziom `Error`, `ExceptionType`) i jedno z `RequestTimingMiddleware` (poziom `Information`, `Method`, `Path`, `StatusCode: 500`, `ElapsedMs`). Nie szukaj treści żądania ani wiadomości wyjątku — celowo nie są zapisywane.
4. **Odczytaj `traceId` z ProblemDetails** i użyj go jako dodatkowego klucza w systemie śledzenia, jeśli jest skonfigurowany. `traceId` w tej paczce nie oznacza, że wdrożono zewnętrzny collector tracingu.
5. **Sprawdź `/health/dependencies`**. Jeśli zwraca 503, sprawdź z poziomu systemu prawa do katalogu ustawionego przez `Diagnostics__StorageDirectory`; nie publikuj pełnych ścieżek ani danych w publicznym zgłoszeniu.
6. **Napraw przyczynę i powtórz żądanie** z nowym Correlation ID, żeby łatwo oddzielić nową próbę od poprzedniej. Kontroluj liczbę 5xx i czas odpowiedzi, nie pojedynczy wpis w izolacji.

Do filtrowania pliku z logami JSON (jeśli jest zainstalowany `jq`) można użyć:

```bash
jq 'select(.State.CorrelationId == "debug-08-123") | {level: .LogLevel, category: .Category, state: .State}' server.jsonl
```

Wdrożenie na serwerze: przekieruj standardowe wyjście procesu do centralnego systemu zbierania logów i włącz retencję z ograniczeniami dostępu. Nie przechowuj haseł ani tokenów JWT w logach. Dostosuj filtr `Microsoft.AspNetCore` w produkcji tak, by inne biblioteki również nie ujawniały danych.

## Uruchomienie testów

Z głównego katalogu paczki:

**Windows / PowerShell**:

```powershell
.\week08\test.ps1
```

**Linux / macOS**:

```bash
./week08/test.sh
```

Lub ręcznie:

```bash
python3 week08/static_check.py
dotnet build week08/tests/Week08.IntegrationTests/Week08.IntegrationTests.csproj -c Release
dotnet run --project week08/tests/Week08.IntegrationTests/Week08.IntegrationTests.csproj -c Release --no-build
```

Test runner wykonuje **55 scenariuszy**, uruchamia API jako osobny proces, analizuje prawdziwe logi JSON, testuje 500 ProblemDetails, prywatność zapytań i trzy środowiska API: Development ze sprawnym katalogiem, Development z niedostępnym katalogiem oraz Production. Testy nie wymagają xUnit ani żadnych zewnętrznych pakietów NuGet. Plik `.github/workflows/week08-ci.yml` uruchamia je automatycznie w GitHub Actions.

**Stan weryfikacji**: w środowisku tworzenia archiwum nie ma zainstalowanego SDK .NET 10, dlatego scenariusze są dostarczone do uruchomienia, ale nie zostały tu wykonane. Raport `TEST_REPORT.md` wyjaśnia, co zweryfikowano statycznie.

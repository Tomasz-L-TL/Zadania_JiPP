# Tydzień 01 — Minimal API — 5/5 ćwiczeń (100 pkt)

Źródło wymagań: [WWSI, semestr 5, week01](https://github.com/marcinpytlik/WWSI_CSharp/blob/main/semestr5/exercises/week01/README.md). Wszystkie pięć zadań zostało zintegrowanych w jednej aplikacji .NET 10. Dla ćwiczenia 4 podstawowe trasy korzystają z prefiksu `/api/v1`; dla zgodności z dosłowną treścią ćwiczeń 1 i 2 zachowano także `/hello` i `/health`.

| Nr | Zadanie | Pkt | Realizacja |
|---|---|---:|---|
| 1 | Hello API | 10 | `GET /hello` (oraz `/api/v1/hello`) → HTTP 200, JSON `{ "message": "..." }` |
| 2 | Health check | 15 | `GET /health` (oraz `/api/v1/health`) → HTTP 200, `status`, `timestampUtc` i `version` |
| 3 | Konfiguracja | 20 | `GET /api/v1/config` → trzy publiczne pola z `appsettings.json`, przez `IOptions<T>` i DTO |
| 4 | Grupowanie | 25 | `MapGroup("/api/v1")`, podgrupa `/notes`, konsekwentne nazwy endpointów |
| 5 | Notes API in-memory | 30 | `GET /notes`, `GET /notes/{id}`, `POST /notes`, `DELETE /notes/{id}` pod `/api/v1`; walidacja |
| | **Łącznie** | **100** | |

## Wymagania

- SDK .NET 10 (C# 14).
- Brak dodatkowych pakietów NuGet, bazy danych i kluczy API.
- Windows, Linux lub macOS.

## Start API

Z katalogu `week01`:

```bash
dotnet run --project src/Week01.Api/Week01.Api.csproj -- --urls=http://localhost:5081
```

Szybka próba:

```bash
curl -i http://localhost:5081/hello
curl -i http://localhost:5081/health
curl -i http://localhost:5081/api/v1/config
curl -i http://localhost:5081/api/v1/notes
curl -i -X POST http://localhost:5081/api/v1/notes -H "Content-Type: application/json" -d '{"title":"Pierwsza notatka","content":"Przyklad"}'
```

Zwrócony przez `POST` identyfikator GUID podstaw w poleceniach GET/DELETE. Możesz również uruchomić plik `src/Week01.Api/Week01.Api.http` w edytorze obsługującym HTTP request files.

## Kontrakt Notes API

| Metoda / ścieżka | Wynik |
|---|---|
| `GET /api/v1/notes` | 200 i JSON lista (na początku `[]`) |
| `GET /api/v1/notes/{id}` | 200 z notatką albo 404 |
| `POST /api/v1/notes` | 201 z notatką i nagłówkiem `Location`; 400 przy błędnych danych |
| `DELETE /api/v1/notes/{id}` | 204 (bez treści) albo 404 |

POST przyjmuje JSON z `title` (obowiązkowy tekst, maksymalnie 100 znaków po przycięciu białych znaków) i `content` (opcjonalny tekst, maksymalnie 2000 znaków). Niepoprawny tytuł/treść powodują HTTP 400 z błędami walidacji. Identyfikatory notatek są typu GUID, a daty są podawane w UTC. Repozytorium jest współbieżne (`ConcurrentDictionary`) i przechowuje dane tylko w pamięci procesu — restart czyści notatki.

## Bezpieczeństwo konfiguracji

`appsettings.json` przechowuje wyłącznie niesekretne dane przykładowe. Odpowiedź `/api/v1/config` jest osobnym DTO (tylko `displayName`, `supportEmail`, `maintenanceMode`). Zamiast zwracać `IConfiguration` lub cały obiekt ustawień, API stosuje listę dozwolonych pól. Testy wstrzykują też sekretne ustawienie przez zmienną środowiskową i sprawdzają, że nie zostaje ujawnione.

## Testy integracyjne

Z katalogu `week01`:

**Windows / PowerShell**:

```powershell
.\test.ps1
```

**Linux / macOS**:

```bash
./test.sh
```

Lub ręcznie:

```bash
dotnet build tests/Week01.IntegrationTests/Week01.IntegrationTests.csproj -c Release
dotnet run --project tests/Week01.IntegrationTests/Week01.IntegrationTests.csproj -c Release --no-build
```

Program testowy wykonuje 18 scenariuszy prawdziwych żądań HTTP: trasy aliasowe i wersjonowane, JSON, UTC i wersję, publiczną konfigurację, inicjalnie pustą listę, błędy walidacji (w tym nieprawidłowy JSON), tworzenie notatki, `Location`, pobranie po ID, listowanie, 404, usuwanie, 204 bez treści oraz stan po usunięciu. Testy uruchamiają osobny proces API na losowym wolnym porcie i zamykają go po zakończeniu. Działają bez xUnit/MSTest/NuGet.

Plik `.github/workflows/week01-ci.yml` uruchamia te same testy na GitHub Actions po pushu lub przy Pull Request.

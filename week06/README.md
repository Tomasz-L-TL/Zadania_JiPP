# WWSI C# — Semestr 5 — Week 06: Relacje i Include

Pełny pakiet rozwiązujący **5 ćwiczeń (100 pkt według treści zadań)** z [repozytorium WWSI](https://github.com/marcinpytlik/WWSI_CSharp/blob/main/semestr5/exercises/week06/README.md). **.NET 10 / ASP.NET Core Minimal API / EF Core 10 / SQLite**. Samodzielny projekt, niezależny od paczek Week 01–05.

## Mapa wymagań

| Zadanie | Pkt | Implementacja |
|---|---:|---|
| 1. Relacja 1:N | 10 | `User → TaskItem`, FK `UserId`, nawigacje i migracja `InitialRelations` |
| 2. Include | 15 | `GET /api/users/{id}/tasks` → `.Include(...)`, 404 oraz DTO |
| 3. ThenInclude | 20 | `GET /api/users/{id}/details` → `User → Tasks → Comments`, DTO trzech poziomów |
| 4. Many-to-many | 25 | `TaskItem ↔ Tag`, tabela `TaskTags` z kluczem `(TaskItemId, TagId)`, `POST /api/tasks/{taskId}/tags/{tagId}` (idempotentne 204) |
| 5. Optymalizacja | 30 | `GET /api/users/{id}/graph` i `/summary`, `AsNoTracking`, `AsSplitQuery`, projekcja `Select`, wydruk SQL i analiza w `SQL_COMPARISON.md` |

## Uruchomienie

Zainstaluj [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Potrzebny jest dostęp do NuGet przy pierwszym `dotnet restore` (pakiety EF Core 10.0.0).

Z katalogu głównego repozytorium (obok folderu `week06`):

```bash
dotnet restore week06/src/Week06.Api/Week06.Api.csproj
dotnet run --project week06/src/Week06.Api/Week06.Api.csproj -- --urls=http://localhost:5086
```

Profil `launchSettings.json` w `dotnet run` ustawia `ASPNETCORE_ENVIRONMENT=Development`. W Development program automatycznie wykonuje dołączoną migrację EF Core i tworzy przykładowe dane, tylko jeśli baza jest pusta. Zostaje zapisany lokalny plik `week06-dev.db`; `.gitignore` zapobiega przypadkowemu commitowi.

## API i oczekiwane statusy

| Metoda | Endpoint | Odpowiedź |
|---|---|---|
| GET | `/health` | 200 |
| GET | `/api/users` | 200, zwięzła lista użytkowników |
| GET | `/api/tags` | 200, lista dostępnych tagów |
| GET | `/api/users/{id}/tasks` | 200, zadania użytkownika bez cykli referencji / 400 / 404 |
| GET | `/api/users/{id}/details` | 200, zadania i komentarze / 400 / 404 |
| GET | `/api/users/{id}/graph` | 200, zadania, komentarze i tagi / 400 / 404 |
| GET | `/api/users/{id}/summary` | 200, lista lekkich DTO + liczniki / 400 / 404 |
| POST | `/api/tasks/{taskId}/tags/{tagId}` | 204 utworzono lub już istniało / 400 / 404 |

### Przykłady

```bash
curl http://localhost:5086/api/users/1/tasks
curl http://localhost:5086/api/users/1/details
curl http://localhost:5086/api/users/1/graph
curl http://localhost:5086/api/users/1/summary
curl -i -X POST http://localhost:5086/api/tasks/1/tags/3
```

W pliku `src/Week06.Api/Week06.Api.http` są gotowe requesty HTTP. Identyfikatory danych przykładowych wyznacza baza; przykłady dla świeżej bazy wskazują pierwszego użytkownika.

## Struktura danych i spójność

- `User 1:N TaskItem` — `Tasks.UserId` FK → `Users.Id`, kasowanie kaskadowe.
- `TaskItem 1:N Comment` — `Comments.TaskItemId` FK → `Tasks.Id`, kasowanie kaskadowe.
- `TaskItem N:N Tag` — tabela pośrednia `TaskTags`, **PK `(TaskItemId, TagId)`**, dwa FK; nie da się zapisać dwóch tych samych par.
- `Tags.Name` jest unikatowe, a FK posiadają indeksy. SQLite provider EF domyślnie wymusza foreign keys.
- Operacja przypisania taga sprawdza istnienie obu encji, zapobiega powtórkom na poziomie kodu i bazy. Powtórzone żądanie zwraca 204 bez tworzenia duplikatu.

Przykładowy seed: Anna (2 zadania, 3 komentarze, kilka tagów) oraz Jan (1 zadanie). Seed jest wykonywany wyłącznie w Development i nie dodaje danych po ponownym starcie.

## Migracje

Migracja w plikach `Data/Migrations/` jest gotowa do uruchomienia. W katalogu `week06`:

```bash
cd week06
dotnet tool restore
dotnet ef migrations list --project src/Week06.Api --startup-project src/Week06.Api
dotnet ef database update --project src/Week06.Api --startup-project src/Week06.Api
```

Nowe migracje twórz zwykłym `dotnet ef migrations add Nazwa --project src/Week06.Api --startup-project src/Week06.Api`. Zmiany w DB wykonuj z kopią zapasową.

**Produkcja:** `appsettings.Production.json` nie zawiera connection stringa. Podaj `ConnectionStrings__Week06Db` jako bezpiecznie zarządzaną zmienną środowiskową i wykonaj migracje osobno, przed uruchomieniem aplikacji. W Production startup **nie uruchamia** migracji ani seeda.

## Testy i analiza SQL

```bash
# Linux/macOS:
bash week06/test.sh

# Windows PowerShell:
.\week06\test.ps1
```

Skrypty wykonują kontrolę statyczną, `dotnet restore`, `dotnet build` i **48 scenariuszy integracyjnych**. Runner tworzy osobny plik SQLite w systemowym folderze tymczasowym, uruchamia API na wolnym porcie, odpytuje realne endpointy, weryfikuje historię migracji, nawigacje, FK, unikatowość relacji, seed, `AsNoTracking`, a następnie drukuje wygenerowany SQL z dwóch metod w `UserQueries.cs`. Po wykonaniu zamyka proces i sprząta bazę.

W `SQL_COMPARISON.md` znajduje się analiza SQL/logowania, działania `AsSplitQuery`, kosztów pełnego grafu i korzyści z projekcji. Wyniki rzeczywistych czasów *nie zostały sfabrykowane*: należy wykonać testy na sprzęcie użytkownika / CI.

Workflow `.github/workflows/week06-ci.yml` uruchamia to samo automatycznie na GitHubie.

## Status weryfikacji

Kontrole strukturalne wykonywane przy przygotowaniu archiwum i stan uruchomienia testów są opisane w `TEST_REPORT.md`. W środowisku generującym nie ma zainstalowanego SDK .NET 10, zatem pomimo przygotowania testów **nie należy uznawać kompilacji ani testów HTTP za zaliczone**, dopóki nie zostaną uruchomione w CI lub lokalnie.

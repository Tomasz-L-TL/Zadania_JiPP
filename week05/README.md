# Week 05 — CRUD z Entity Framework Core (.NET 10)

Rozwiązania **wszystkich 5 ćwiczeń (100 pkt według punktacji zadań)** z [oryginalnego repozytorium WWSI](https://github.com/marcinpytlik/WWSI_CSharp/blob/main/semestr5/exercises/week05/README.md). Samodzielne ASP.NET Core Minimal API, EF Core 10, SQLite, migracja `InitialCreate`, walidacja, DTO oraz 60 scenariuszy integracyjnych HTTP / SQLite.

## Mapa wymagań

| Ćwiczenie | Punkty | Rozwiązanie |
|---|---:|---|
| 1. Create | 10 | `POST /users`, `SaveChangesAsync`, `201 CreatedAtRoute`, nagłówek `Location` |
| 2. Read | 15 | `GET /users`, `GET /users/{id:int}`, `AsNoTracking`, `404` |
| 3. Update | 20 | `PUT /users/{id}` i dodatkowo `PATCH /users/{id}`, walidacja, asynchroniczny zapis, `404/200` |
| 4. Delete | 25 | `DELETE /users/{id}`, `Remove`, `SaveChangesAsync`, `404/204` |
| 5. CRUD produkcyjny | 30 | oddzielne DTO, metadata paginacji, wyszukiwanie, filtr wieku, sortowanie, walidacja parametrów, ochrona przed overposting |

**100 pkt** to maksymalna suma punktów z zadania, nie potwierdzona ocena akademicka.

## Wymagania

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Połączenie z NuGet przy pierwszym `dotnet restore` — pakiety EF Core SQLite i Design 10.0.0
- Dla `dotnet ef`: lokalne narzędzie z `week05/.config/dotnet-tools.json`

## Uruchomienie

Z **katalogu głównego rozpakowanego archiwum**:

```bash
dotnet run --project week05/src/Week05.Api/Week05.Api.csproj -- --urls=http://localhost:5085
```

`launchSettings.json` włącza `Development`, a `appsettings.Development.json` wskazuje lokalną bazę `week05-dev.db` i migrację przy starcie. Baza jest plikiem (nie jest dodawana do Gita). Produkcja nie uruchamia migracji automatycznie i wymaga zmiennej środowiskowej `ConnectionStrings__UsersDb`.

### Przykłady API

Utworzenie użytkownika:

```http
POST /users
Content-Type: application/json

{ "name": "Anna", "email": "anna@example.com", "age": 25 }
```

Odpowiedź: `201 Created`, `Location: /users/1` (albo równoważny pełny URL), JSON z `id`, `name`, `email`, `age`, `createdAtUtc`, `updatedAtUtc`.

Lista z paginacją, filtrowaniem i sortowaniem:

```http
GET /users?search=anna&minAge=18&maxAge=40&page=1&pageSize=10&sortBy=name&sortDir=desc
```

Odpowiedź:

```json
{
  "items": [
    {
      "id": 1,
      "name": "Anna",
      "email": "anna@example.com",
      "age": 25,
      "createdAtUtc": "2026-10-26T10:00:00Z",
      "updatedAtUtc": "2026-10-26T10:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalItems": 1,
  "totalPages": 1,
  "sortBy": "name",
  "sortDir": "desc"
}
```

*Powyższe znaczniki czasu to wyłącznie przykładowe wartości.*

| Metoda | Endpoint | Rezultat |
|---|---|---|
| POST | `/users` | 201, 400 przy błędnych danych, 409 przy powtórnym e-mailu |
| GET | `/users` | 200, metadane i lista |
| GET | `/users/{id}` | 200, 400 przy id ≤ 0, 404 przy braku |
| PUT | `/users/{id}` | 200, 400, 404 lub 409 |
| PATCH | `/users/{id}` | 200, 400, 404 lub 409 |
| DELETE | `/users/{id}` | 204, 400 lub 404 |
| GET | `/health` | 200 |

### Walidacja i parametry

- `name`: wymagane, maks. 100 znaków (po przycięciu białych znaków).
- `email`: wymagane, poprawny format, maks. 200 znaków; adresy unikatowe niezależnie od wielkości liter (dodatkowy indeks unikalny w SQLite).
- `age`: wymagany, przedział **18–120**.
- `page`: 1–100000, domyślnie 1; `pageSize`: 1–100, domyślnie 10.
- `search`: opcjonalny, maks. 100 znaków, dopasowuje do imienia/nazwiska lub e-maila, ignoruje wielkość liter.
- `minAge`, `maxAge`: opcjonalne, granice 18–120, `minAge <= maxAge`.
- `sortBy`: `id` (domyślnie), `name`, `email`, `age`, `createdAt`.
- `sortDir`: `asc` (domyślnie), `desc`. Każda sortowana strona ma deterministyczną kolejność przez pomocniczy sort po `id`.

Niepoprawne dane zwracają HTTP 400 w formacie `ValidationProblem` z błędami przypisanymi do pól. Powtórny adres e-mail zwraca HTTP 409 z kodem `duplicate_email`. Przy `PATCH` zmieniają się tylko podane pola — pusty obiekt jest odrzucany.

`CreateUserRequest`, `UpdateUserRequest` i `PatchUserRequest` **nie zawierają `Id` ani `IsAdmin`**. Zwracamy osobny `UserResponse`, a nie encję EF. Wartości `id` czy `isAdmin` podane dodatkowo w JSON nie mogą zmodyfikować encji.

## Testy

**Linux/macOS** (z root archiwum):

```bash
bash week05/test.sh
```

**Windows PowerShell**:

```powershell
.\week05\test.ps1
```

Alternatywnie:

```bash
python3 week05/static_check.py
dotnet build week05/tests/Week05.IntegrationTests/Week05.IntegrationTests.csproj -c Release
dotnet run --project week05/tests/Week05.IntegrationTests/Week05.IntegrationTests.csproj -c Release --no-build
```

Testy uruchamiają proces API na wolnym porcie, tworzą fizyczną, tymczasową bazę SQLite, wykonują **60 scenariuszy HTTP oraz odczytów z bazy**, a następnie sprzątają proces i plik. Sprawdzają operacje CRUD, 201 + Location, 400/404/409/204, filtrowanie, sortowanie, paginację, zapis przez EF, migrację oraz overposting. **Wyniki uruchomieniowe wymagają potwierdzenia lokalnie albo w GitHub Actions.**

Workflow: `.github/workflows/week05-ci.yml`, uruchamiany na push / pull request zawierający zmiany w week05 oraz ręcznie.

## Migracje i Production

W katalogu `week05`:

```bash
cd week05
dotnet tool restore
dotnet ef migrations list --project src/Week05.Api --startup-project src/Week05.Api
dotnet ef database update --project src/Week05.Api --startup-project src/Week05.Api
```

Migracja `202610260001_InitialCreate` jest już dołączona do projektu, razem z modelem docelowym i snapshotem. Dodając kolejną migrację użyj `dotnet ef migrations add NazwaMigracji --project src/Week05.Api --startup-project src/Week05.Api`.

**Produkcja:** plik `appsettings.Production.json` nie zawiera żadnych danych połączenia ani sekretów. Należy je ustawić bezpiecznie poza repozytorium, np. w menedżerze sekretów, wystawiając jako `ConnectionStrings__UsersDb`; zastosuj migracje jako osobny etap wdrożenia i wykonaj backup. Przykładowe zapytania mają tylko charakter dydaktyczny — przed publicznym wdrożeniem dodaj autoryzację, limity żądań i monitoring.

## GitHub

Skopiuj zawartość paczki do głównego katalogu własnego repozytorium. Zachowaj istniejący główny `README.md` i `.gitignore` poprzez scalenie. Koniecznie dodaj folder `week05` i `.github/workflows/week05-ci.yml`.

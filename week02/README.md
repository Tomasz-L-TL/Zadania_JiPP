# Tydzień 2 — Routing i parametry (.NET 10)

Źródło zadań: https://github.com/marcinpytlik/WWSI_CSharp/blob/main/semestr5/exercises/week02/README.md

## Mapa wymagań: 100 pkt

| Ćwiczenie | Punkty | Realizacja |
|---|---:|---|
| 1. Route parameter | 10 | `GET /users/{id:int}`, id <= 0 → 400, nieznany → 404, istniejący → 200, błędy jako `ApiError` |
| 2. Query parameters | 15 | `GET /users?search=&page=&pageSize=`, domyślne wartości, walidacja zakresu, filtrowanie i paginacja |
| 3. Route groups | 20 | `MapGroup("/users")`, `MapGroup("/tasks")`; osobne klasy endpointów |
| 4. Typed results | 25 | `TypedResults`, `Results<Ok<...>, BadRequest<...>, NotFound<...>>` + jawne warianty statusów |
| 5. Wersjonowanie kontraktu | 30 | `/api/v1/products`, `/api/v2/products`, osobne DTO, wspólne `IProductCatalog` |

## Tabela tras

| Metoda | Ścieżka | 200 | 400 | 404 |
|---|---|---|---|---|
| GET | `/users/{id:int}` | istniejący użytkownik | id <= 0 | brak / nieliczbowe id |
| GET | `/users` | lista z metadanymi paginacji | niepoprawny query | — |
| GET | `/tasks` | lista zadań | — | — |
| GET | `/tasks/{id:int}` | istniejące zadanie | id <= 0 | brak / nieliczbowe id |
| GET | `/api/v1/products` | lista produktów v1 | — | — |
| GET | `/api/v1/products/{id:int}` | produkt v1 | id <= 0 | brak / nieliczbowe id |
| GET | `/api/v2/products` | lista produktów v2 | — | — |
| GET | `/api/v2/products/{id:int}` | produkt v2 | id <= 0 | brak / nieliczbowe id |

Ważne: int route constraint oznacza, że `/users/abc` **nie pasuje do trasy i zwraca 404**, a `/users/0` pasuje i zwraca **400** z komunikatem o błędzie walidacji.

### Parametry `/users`

- `search`: opcjonalny tekst, do 100 znaków; szuka w imieniu/nazwisku i e-mailu, ignoruje wielkość liter; spacje na końcach są usuwane.
- `page`: domyślnie `1`, dopuszczalnie 1–10000.
- `pageSize`: domyślnie `10`, dopuszczalnie 1–50.
- Nieprawidłowy format liczby (`page=abc`) powoduje automatyczne HTTP 400 w ASP.NET Core.
- Kolejność jest deterministyczna (rosnąco po ID), a `totalItems` i `totalPages` dotyczą wyników po filtrowaniu.

Przykładowe żądanie:

```http
GET /users?search=anna&page=2&pageSize=1
```

Przykładowa odpowiedź HTTP 200:

```json
{
  "items": [{ "id": 11, "name": "Anna Lis", "email": "anna.lis@example.com" }],
  "page": 2,
  "pageSize": 1,
  "totalItems": 2,
  "totalPages": 2
}
```

Przykładowa odpowiedź HTTP 404 na `/users/999`:

```json
{
  "code": "user_not_found",
  "message": "Uzytkownik o ID 999 nie istnieje."
}
```

### Wersjonowanie produktów

**v1** pozostaje zgodna z wcześniejszym kontraktem, w którym cena jest liczbą:

```http
GET /api/v1/products/1
```

```json
{ "id": 1, "name": "Klawiatura", "price": 149.90, "available": true }
```

**v2** ma osobną strukturę ceny, kategorię i właściwość `inStock`:

```http
GET /api/v2/products/1
```

```json
{
  "id": 1,
  "name": "Klawiatura",
  "price": { "amount": 149.90, "currency": "PLN" },
  "category": "Akcesoria",
  "inStock": true
}
```

Wszystkie dane produktów są zapisane wyłącznie w `SeededProductCatalog`; endpointy korzystają z tego samego `IProductCatalog` i tylko mapują produkt domenowy na inne DTO. Istniejący klient korzystający z v1 nie musi zmieniać kodu.

## Struktura projektu

```text
week02/
├── src/Week02.Api/
│   ├── Program.cs
│   ├── Contracts/ApiContracts.cs
│   ├── Domain/Entities.cs
│   ├── Repositories/Catalogs.cs
│   ├── Endpoints/
│   │   ├── UserEndpoints.cs
│   │   ├── TaskEndpoints.cs
│   │   └── ProductEndpoints.cs
│   ├── Week02.Api.csproj
│   └── Week02.Api.http
├── tests/Week02.IntegrationTests/
│   ├── Program.cs
│   └── Week02.IntegrationTests.csproj
├── test.sh
├── test.ps1
├── TEST_REPORT.md
└── README.md
```

## Szybki start

```bash
# w katalogu głównym archiwum
dotnet run --project week02/src/Week02.Api/Week02.Api.csproj -- --urls=http://localhost:5082
```

W drugim terminalu wykonaj `./week02/test.sh` (bash) albo `./week02/test.ps1` (PowerShell). Testy używają własnego, losowego portu, więc mogą działać równolegle z API na 5082.

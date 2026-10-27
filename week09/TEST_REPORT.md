# Raport weryfikacji — Week 09

**Data:** 2026-10-26.

## Mapa wymagań (100 pkt łącznie)

| Ćwiczenie | Punkty | Dowód w kodzie/testach |
|---|---:|---|
| WebApplicationFactory + GET /health | 10 | `TestApiFactory`, `Health_returns_200_and_expected_JSON_body` |
| 404 zasobu | 15 | `Missing_note_returns_404_independent_of_data`, `Unknown_endpoint_returns_404_with_correlation_id` |
| POST poprawny / błędny | 20 | `Successful_POST_creates_a_note_with_201_and_Location`, `Invalid_POST_returns_400_with_validation_errors` |
| Podmiana DI / SQLite-in-memory | 25 | `ConfigureTestServices`, usunięcie starych `DbContextOptions` i ich konfiguratorów, pojedyncze połączenie na fabrykę, cleanup po Dispose |
| Pakiet >=15 testów CRUD, auth, validation, middleware | 30 | `IntegrationTests.cs`: **39 scenariuszy `[Fact]`** ze sprawdzaniem happy/error path |

Łączna punktacja ćwiczeń to nie oficjalnie przyznana ocena.

## Co można potwierdzić bez SDK

- **104/104 kontroli statycznych zaliczonych** (`tools/static_check.py`, pełne wyniki w `STATIC_CHECK_RESULTS.txt`): obecność plików, poprawność XML `.csproj`, JSON, struktura kodu i obecność testów.
- Kontrola integralności archiwum ZIP i braku skompilowanych artefaktów: wykonana podczas finalnego pakowania.
- **39** osobnych metod `[Fact]` w źródłach (liczba deklaracji, **nie** liczba zaliczonych testów).
- YAML workflow poprawnie parsowany; 7+ kroków, w tym build/test i archiwizacja wyników TRX.
- Składnia `bash -n week09/test.sh` poprawna.

## Czego nie zweryfikowano w tym środowisku

- Nie wykonano `dotnet restore`, `dotnet build` ani `dotnet test`: .NET SDK nie jest tu zainstalowane, a kontener nie ma działającego dostępu do serwerów pakietów.
- Nie uruchomiono testów HTTP ani testów z bazą SQLite-in-memory. Nie podajemy wyników tych testów jako zaliczonych.

## Jak uruchomić rzeczywistą weryfikację

```bash
python3 week09/tools/static_check.py
dotnet restore week09/tests/Week09.IntegrationTests/Week09.IntegrationTests.csproj
dotnet test week09/tests/Week09.IntegrationTests/Week09.IntegrationTests.csproj -c Release --logger 'console;verbosity=normal'
```

Po pushu wejdź na GitHub → **Actions** → `Week 09 - Integration tests`; zielony wynik kompilacji i testów oznacza rzeczywistą weryfikację. Workflow archiwizuje wynik TRX.

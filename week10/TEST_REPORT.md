# Raport weryfikacji — Week 10 (Users + Tasks API)

Data: 26.10.2026.

## Stan przygotowania

- Zaimplementowano **5 ćwiczeń** z README źródłowego (100 **możliwych** pkt, nie jest to ocena od prowadzącego).
- Przygotowano **49 testów xUnit `[Fact]`** w `tests/Week10.IntegrationTests/ApiTests.cs` opartych o `WebApplicationFactory<Program>`.
- Baza testowa jest fizycznie niezależna od deweloperskiej: otwarte `SqliteConnection("Data Source=:memory:")` per instancja fabryki. Podczas uruchomienia API wykonywana jest migracja, seed testowych tagów i administratora.
- Dostarczono migrację `InitialCreate` wraz z modelem docelowym (`Designer.cs`) i snapshotem EF Core 10.
- Migracje, CRUD, JWT, admin policy, ownership, 4 raporty, obsługa błędów i strukturalne logi są obecne w kodzie.
- Kontrole statyczne: **104/104 PASS** (`STATIC_CHECK_RESULTS.txt`). Poprawność JSON, XML i YAML dodatkowo sprawdzona. Integralność archiwum zweryfikowana podczas pakowania.

## Ograniczenie środowiska

**Nie było możliwości wykonania `dotnet restore`, `dotnet build`, `dotnet test` ani uruchomienia API.** W środowisku przygotowującym paczkę nie jest dostępne SDK .NET 10, a próba dostępu do serwera instalacyjnego zakończyła się błędem DNS. Sprawdzenia statyczne **nie dowodzą kompilowalności ani zaliczenia testów**. Wyniki uruchomienia muszą być potwierdzone lokalnie lub w GitHub Actions.

## Polecenia uruchomieniowe

```bash
# Repo root (Linux/macOS)
./week10/test.sh

# Repo root (Windows PowerShell)
./week10/test.ps1

# Albo bezpośrednio
dotnet restore week10/Week10.slnx
dotnet build week10/Week10.slnx -c Release --no-restore
dotnet test week10/tests/Week10.IntegrationTests/Week10.IntegrationTests.csproj -c Release --no-build
```

Repozytorium GitHub: wejdź w **Actions → Week 10 - Final Users Tasks API** i sprawdź wyniki build/test.

## Pokrycie testów

1. Rejestracja, odpowiedź 201, nagłówek Location, brak hasła/hash w DTO, hashowanie w DB i unikalność case-insensitive.
2. Walidacja wejścia oraz logowanie JWT, terminy i tokeny, 401/403, polityka AdminOnly.
3. CRUD użytkowników i uprawnienia do własnego konta, lista admina z paginacją i filtrem.
4. CRUD zadań, statusy, przypisanie właściciela, brak możliwości odczytu i usunięcia cudzych zadań.
5. Relacja N:M tagów i bezpieczna aktualizacja relacji (bez duplikatów).
6. Cztery raporty: po statusie, po użytkowniku, top tagi i zaległe zadania.
7. Zastosowana migracja i izolacja danych między instancjami fabryki.

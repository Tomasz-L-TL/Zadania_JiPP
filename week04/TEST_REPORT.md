# Raport weryfikacji — Week 04

Data przygotowania: 2026-10-26.

## Zakres

- 5 ćwiczeń, 100 punktów możliwych do uzyskania według opisu oryginalnych zadań.
- Migracja EF Core, model, Fluent API, seed danych, konfiguracje środowisk i instrukcje `dotnet ef`.
- Testy integracyjne SQLite + test smoke HTTP, skrypty uruchomieniowe i workflow GitHub Actions.

## Status wykonania

| Rodzaj | Status |
|---|---|
| Parsowanie XML projektów, JSON i kontrola struktury | **Zaliczone lokalnie** — wszystkie pliki JSON i oba projekty `.csproj` poprawnie parsowane |
| Kontrole statyczne projektu | **Zaliczone lokalnie — 85/85** (`STATIC_CHECK_RESULTS.txt`) |
| Walidacja YAML workflow | **Zaliczone lokalnie** — GitHub Actions YAML prawidłowo parsowany |
| Rzeczywista kompilacja z .NET 10 | **Nieuruchomiona lokalnie: brak zainstalowanego SDK .NET 10** |
| Uruchomienie testów EF Core SQLite | **Nieuruchomione lokalnie: brak SDK .NET 10** |
| HTTP smoke z uruchomionym API | **Nieuruchomiony lokalnie: brak SDK .NET 10** |
| GitHub Actions | Przygotowano, wynik po push |

Testy dostarczone z rozwiązaniem nie stanowią dowodu ich poprawnego przejścia bez wykonania ich na komputerze z .NET 10 lub w CI.

## Dostarczone scenariusze

- **22 testy integracyjne** w `Week04.IntegrationTests/Program.cs` (wymagają kompilacji i uruchomienia).
- **1 test smoke HTTP** sprawdzający trzy rekordy po starcie w Development.
- Kontrole statyczne potwierdzają strukturę, metadane modelu, pliki środowiskowe, dokumentację i obecność testów, ale nie stanowią dowodu poprawnej kompilacji.

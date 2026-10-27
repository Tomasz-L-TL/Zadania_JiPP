# Raport weryfikacji — Week 02

## Status

- **Kontrole statyczne:** 33/33 **PASS** (uruchomiono `python3 week02/static_check.py`).
- **Plik workflow GitHub Actions:** poprawna struktura YAML; odczytany z powodzeniem w lokalnym walidatorze.
- **Skrypt bash:** `bash -n week02/test.sh` — **PASS**.
- **Automatyczne testy integracyjne HTTP:** przygotowano **42 unikatowe scenariusze**. **Nie uruchomiono** ich w środowisku przygotowującym paczkę, ponieważ nie jest dostępny .NET 10 SDK ani kompilator C#.
- **Budowa projektu C#:** **nie wykonana** z tego samego powodu. Nie należy traktować sukcesu kontroli statycznych jako potwierdzenia kompilacji ani przejścia 42 testów HTTP.

## Co zostało sprawdzone statycznie

1. Obecność kompletnego kodu, testów, skryptów, instrukcji, przykładów HTTP i workflow.
2. Poprawny XML obu projektów; `net10.0` oraz prawidłowe typy SDK/projektu.
3. Brak zależności od zewnętrznych pakietów NuGet.
4. Rejestracja wszystkich repozytoriów w kontenerze DI i grup endpointów w programie.
5. Implementacja wszystkich tras wymaganych w tygodniu 2.
6. Deklaracje `Results<Ok<...>, BadRequest<...>, NotFound<...>>`.
7. Domyślne parametry `search`, `page`, `pageSize`, ich walidacja, filtrowanie i paginacja.
8. Niezależne DTO v1 i v2 produktów oraz jedno współdzielone repozytorium domenowe.
9. Deterministyczne przykładowe dane w pamięci.
10. Obecność 42 kolejno numerowanych scenariuszy HTTP, start i zakończenie procesu API przez zestaw testów.
11. Konfiguracja GitHub Actions z .NET 10, budowaniem i uruchomieniem testów.
12. Spójność dokumentacji i brak plików `bin/` i `obj/` w archiwum.

Pełna, reprodukowalna lista 33 kontroli i ich wyników znajduje się w konsolowym wyjściu `python3 week02/static_check.py`.

## 42 scenariusze HTTP

- Testy **01–06**: identyfikator użytkownika, `int` constraint, `200` / `400` / `404`.
- Testy **07–24**: domyślne wartości, stronicowanie, `search`, limity parametrów i błędy konwersji.
- Testy **25–30**: endpointy `/tasks`, brak konfliktów tras.
- Testy **31–42**: osobne kontrakty v1/v2, wspólne dane, wersjonowanie oraz odpowiedzi `200` / `400` / `404`.

## Wykonanie po rozpakowaniu

Zainstaluj **.NET 10 SDK**, przejdź do katalogu głównego archiwum i uruchom:

**Windows PowerShell:**

```powershell
.\week02\test.ps1
```

**Linux/macOS (bash):**

```bash
./week02/test.sh
```

Po przesłaniu do GitHuba analogiczną weryfikację przeprowadzi workflow **Week 02 - Build and HTTP integration tests**. Dopiero jego zielony wynik jest potwierdzeniem, że rozwiązanie zbudowało się i testy zakończyły się sukcesem.

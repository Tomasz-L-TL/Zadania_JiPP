# Week 05 — raport weryfikacji

Data przygotowania: 2026-10-26.

| Kategoria | Wynik |
|---|---|
| Pokrycie pięciu ćwiczeń | Implementacje przygotowane |
| Statyczne sprawdzenie plików | **76/76 zaliczonych**; zapis w `STATIC_CHECK_RESULTS.txt` |
| Przykładowe JSON i składnia XML | Sprawdzane statycznie |
| 60 scenariuszy HTTP + SQLite | Kod testowy przygotowany, ale **nieuruchomiony w tym środowisku** |
| Kompilacja .NET 10 | **Nieuruchomiona** — SDK .NET 10 niedostępne |
| GitHub Actions | Workflow przygotowany; jego wynik wymaga push i uruchomienia CI |

**Nie należy traktować sprawdzenia statycznego jako potwierdzenia, że program kompiluje się lub przechodzi testy wykonawcze.** Użytkownik może uruchomić `week05/test.sh` albo `week05/test.ps1`, lub sprawdzić wyniki GitHub Actions.

Zakres testów: asynchroniczny CRUD, statusy 201/200/204/400/404/409, Location dla POST, walidacja danych, paginacja i metadane, filtrowanie po nazwie/e-mailu i wieku, sortowanie po kilku polach, brak overposting (Id/IsAdmin), duplikaty case-insensitive, indeks SQLite, migracja oraz trwałość zapisu.

## Dodatkowe sprawdzenia wykonane lokalnie

- XML obu projektów: poprawny.
- Pliki konfiguracyjne JSON: poprawne.
- Przykładowe żądania z JSON w `.http`: 4/4 poprawne.
- Składnia YAML GitHub Actions: poprawna.
- Pliki C#: poprawne sparowanie nawiasów według leksera składni (11/11 plików). **Nie jest to kompilacja.**
- Skrypt Bash: `bash -n` bez błędów.

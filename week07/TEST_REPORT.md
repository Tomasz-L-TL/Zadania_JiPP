# Raport weryfikacji — Week 07

Data: 2026-10-26

## Zakres

- Zadania 1–5 (łącznie 100 pkt wg arkusza): rejestracja i hashowanie, login JWT, autoryzacja, role i polisy, bezpieczna konfiguracja i rotacja sekretu.
- Implementacja API, plik żądań HTTP, konfiguracja środowiskowa, własne testy integracyjne, skrypty, workflow GitHub Actions.

## Weryfikacja

- Kontrole statyczne źródła, XML, YAML, JSON i obecności testów: **100/100 sprawdzeń zaliczonych**; pełne wyniki w `STATIC_CHECK_RESULTS.txt`.
- **60 scenariuszy HTTP zostało przygotowanych, ale nie uruchomiono ich** — na hoście brakuje .NET 10 SDK i możliwości pobrania narzędzia.
- **Kompilacja API/testów nie została wykonana**. Statyczna weryfikacja nie potwierdza kompilacji.
- CI dostarczono, ale zielony wynik będzie możliwy do zweryfikowania dopiero po uruchomieniu na GitHubie.

## Jak potwierdzić wynik

Po wgraniu paczki uruchom `week07/test.ps1` na Windows lub `week07/test.sh` na Linux/macOS albo otwórz GitHub → **Actions** → **Week 07 — JWT and authorization**. Testy używają wyłącznie losowych kluczy i haseł tworzonych podczas wykonania; nie trzeba ujawniać sekretów w repozytorium.

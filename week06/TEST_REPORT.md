# Raport weryfikacji Week 06

**Data przygotowania:** 2026-10-26.

- Zadania: komplet pięciu ćwiczeń (punkty 10 + 15 + 20 + 25 + 30 = 100 według treści zadań).
- Kod: .NET 10, ASP.NET Core Minimal API, EF Core 10, SQLite.
- Migracja `202610260001_InitialRelations` z designerem i zamrożonym snapshotem modelu.
- **48 scenariuszy integracyjnych przygotowanych** — testy rzeczywistego HTTP, relacji SQLite i SQL generowanego przez EF Core.
- **Kompilacja / wykonanie 48 scenariuszy lokalnie: NIE WYKONANO**, ponieważ w środowisku tworzącym archiwum nie ma .NET 10 SDK i nie można go pobrać przez sieć.
- Kontrole statyczne: **110/110 zaliczonych**; wynik każdej kontroli znajduje się w `STATIC_CHECK_RESULTS.txt`. Zbadano także składnię YAML oraz skrypt Bash. Archiwum ZIP przechodzi dodatkowe sprawdzenie CRC przy pakowaniu.
- Faktyczny wynik kompilacji i 48 testów należy potwierdzić przez GitHub Actions lub `week06/test.sh` / `week06/test.ps1`.

### Co obejmuje 48 testów

- 01–10: `/health`, listy i `Include` — statusy, typowe i niepoprawne identyfikatory.
- 11–15: `ThenInclude` — trójpoziomowe DTO, komentarze, 404.
- 16–23: pełny graf i projekcja — tagi, agregaty, minimalizacja DTO, 400/404.
- 24–32: przypisywanie tagów, idempotencja, brak duplikatów i zgodność odczytów.
- 33–37: historia migracji i metadane relacji/FK/PK.
- 38–41: `ToQueryString()` i `AsNoTracking()`.
- 42–45: realne constraints SQLite (kompozytowy PK i FK).
- 46–48: idempotentny seed, ochrona Production, kompletność hierarchii.

### Test produkcyjny

Dane demo i automatyczne migracje w Production są wyłączone. `ConnectionStrings__Week06Db` musi zostać dostarczone przez środowisko, bez commitowania haseł/plików DB.

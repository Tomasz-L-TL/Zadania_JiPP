# Raport weryfikacji — WWSI Week 03

**Data przygotowania:** 2026-10-25

## Zakres zadań

| Ćwiczenie | Punkty | Rozwiązanie | Sprawdzenie |
|---|---:|---|---|
| DTO wejściowe | 10 | `CreateUserRequest`, domenowy `User`, `UserResponse`, mapowanie w serwisie | Statycznie + scenariusze API 04–09 |
| DataAnnotations | 15 | Required, StringLength, Range, EmailAddress, Regex, komunikaty | Statycznie + scenariusze 16–36 |
| Walidacja biznesowa | 20 | atomowy `TryAdd` loginu, status 409, logika serwisu | Statycznie + scenariusze 10–15 i 57–58 |
| ProblemDetails | 25 | 400 ValidationProblem (błędy pól), 409 ProblemDetails | Statycznie + scenariusze 13, 30–32, 49, 54–55, 59–60 |
| Pipeline walidacji | 30 | `ValidationFilter<TRequest>`, `IRequestValidator`, dwa endpointy | Statycznie + scenariusze 16–36, 42–53 |

## Co sprawdzono, a czego nie

- **Kontrola statyczna**: **77/77 sprawdzeń zaliczonych** (`python3 week03/static_check.py`) — sprawdza kompletność, strukturę, charakterystyczne elementy implementacji, przykłady JSON i składnię XML projektów.
- **Testy HTTP**: **60 scenariuszy przygotowanych**, ale **nieuruchomionych** w środowisku generowania (brak SDK .NET 10).
- **Kompilacja C#**: **nieuruchomiona** w środowisku generowania (brak SDK .NET 10).
- **Workflow GitHub Actions**: dostarczony, ale jego wynik można poznać dopiero po uruchomieniu w GitHubie.
- **Ocena oficjalna**: 100 punktów to łączna punktacja zadań, a nie przyznana ocena.

## Jak uzyskać rzeczywistą weryfikację

Po wrzuceniu paczki na GitHuba otwórz kartę **Actions** → workflow **Week 03 - Validation - Build and HTTP tests**. Powinien wykonać kontrolę statyczną, dwie kompilacje oraz 60 testów integracyjnych. Zielony status po wykonaniu oznacza, że kod przeszedł weryfikację w .NET 10. Alternatywnie uruchom `week03/test.ps1` lub `week03/test.sh` lokalnie.

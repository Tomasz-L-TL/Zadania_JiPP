# Raport weryfikacji — Week 08

Przygotowano kompletny projekt ASP.NET Core Minimal API .NET 10 z pięcioma wymaganymi rozwiązaniami i 55 scenariuszami testów.

## Zastosowany podział

| Zadanie | Punkty | Odpowiedni plik |
|---|---:|---|
| Request timing | 10 | `src/Week08.Api/Middleware/RequestTimingMiddleware.cs` |
| Correlation ID | 15 | `src/Week08.Api/Middleware/CorrelationIdMiddleware.cs` |
| Global exception handling | 20 | `src/Week08.Api/Middleware/GlobalExceptionMiddleware.cs` |
| Structured logging | 25 | `src/Week08.Api/Program.cs` + `Middleware/SafeLogIdentity.cs` |
| Observability mini-stack | 30 | `src/Week08.Api/Diagnostics/*` + `README.md` |

## Stan kontroli

- Kontrole statyczne: **101/101 zaliczonych**; szczegóły w `STATIC_CHECK_RESULTS.txt`.
- Format plików JSON/XML/YAML i składnia skryptu bash: **zaliczone**. Integralność ZIP jest sprawdzana osobno podczas pakowania.
- **55 scenariuszy integracyjnych:** przygotowano, lecz nie wykonano w środowisku tworzenia pliku — brak SDK .NET 10.
- **Kompilacja C#:** nie wykonano w tym środowisku z tego samego powodu.
- **GitHub Actions:** gotowy workflow; wynik pojawi się po push do repozytorium.

**Kontrola struktury nie jest równoważna kompilacji lub działającym testom**. Aby to potwierdzić, uruchom `./week08/test.sh` lub `./week08/test.ps1` na komputerze z .NET 10 SDK albo sprawdź wynik zakładki GitHub Actions.

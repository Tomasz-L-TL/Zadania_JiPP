# WWSI — Semestr 5, Week 07: JWT i autoryzacja

Źródło: [ćwiczenia Week 07](https://github.com/marcinpytlik/WWSI_CSharp/blob/main/semestr5/exercises/week07/README.md). **5 ćwiczeń, 100 punktów według punktacji zadania**. Rozwiązanie demonstracyjne oparte na .NET 10 Minimal API, Microsoft.AspNetCore.Authentication.JwtBearer 10.0.0 i hashowaniu haseł przez ASP.NET Core Identity. Nie wymaga Week 01–06.

## Mapa zadań

| Ćwiczenie | Punkty | Realizacja |
|---|---:|---|
| 1. Rejestracja | 10 | `POST /api/auth/register`: DataAnnotations, unikatowy e-mail, PBKDF2 (`PasswordHasher<T>`), odpowiedź `201` bez hasła i hasha |
| 2. Logowanie | 15 | `POST /api/auth/login`: weryfikacja hasha, JWT HS256, claims `sub`, `email`, `role`, `jti`, `401` |
| 3. Authorize | 20 | `GET /api/users/me` zabezpieczony, `GET /health` publiczny, 401 bez tokena |
| 4. Role/claim | 25 | Polityka `AdminOnly`, `GET /api/users`, `GET /api/users/{id:int}` oraz `DELETE /api/users/{id:int}` wyłącznie dla Admin, 403 dla User |
| 5. Konfiguracja | 30 | Klucz Base64 z env/User Secrets, walidacja issuer/audience/expiry/alg, rotacja `PreviousSigningKeys`, dokumentacja 401/403 |

**100 punktów to suma wartości ćwiczeń, a nie przyznana ocena.**

## Wymagania

- [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Dostęp do NuGet przy pierwszym `dotnet restore` dla pakietu JWT Bearer 10.0.0
- Nie ma bazy danych: konta i hashe przechowywane są wyłącznie w pamięci procesu. Restart kasuje konta.

## Uruchomienie bez wpisywania klucza do repozytorium

Z katalogu głównego (tego, który zawiera `week07/`).

**PowerShell**:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$key = [byte[]]::new(64)
[System.Security.Cryptography.RandomNumberGenerator]::Fill($key)
$env:Jwt__SigningKey = [Convert]::ToBase64String($key)
dotnet run --project week07/src/Week07.Api/Week07.Api.csproj -- --urls=http://localhost:5087
```

**Linux/macOS bash**:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export Jwt__SigningKey="$(openssl rand -base64 64 | tr -d '\n')"
dotnet run --project week07/src/Week07.Api/Week07.Api.csproj -- --urls=http://localhost:5087
```

Sekret musi zawierać co najmniej 32 **losowe** bajty (przykłady generują 64). Jest dekodowany z Base64. Jeśli brakuje sekretu, jest niepoprawny lub za krótki, aplikacja **nie uruchomi się**. W `appsettings.json` są tylko niesekretne `Issuer`, `Audience` i `ExpiryMinutes=15`. Tokeny podpisuje algorytm HS256, a ich ważność wynosi 15 minut, z 30-sekundowym tolerowanym odchyleniem zegarów. Zmiana klucza podczas restartu unieważnia stare JWT, chyba że poprzedni klucz skonfigurowano do przejściowej weryfikacji.

### Opcjonalny administrator tylko dla Development

Rejestracja publiczna nigdy nie przyznaje roli Admin. Dla demonstracji polityki `AdminOnly` można przed uruchomieniem aplikacji dodatkowo ustawić dwie **osobne zmienne środowiskowe**:

```powershell
$env:BootstrapAdmin__Email = 'admin@example.test'
$securePassword = Read-Host 'Wpisz silne hasło administratora' -AsSecureString
$env:BootstrapAdmin__Password = [System.Net.NetworkCredential]::new('', $securePassword).Password
```

```bash
export BootstrapAdmin__Email='admin@example.test'
read -r -s -p 'Hasło administratora: ' BootstrapAdmin__Password; echo
export BootstrapAdmin__Password
```

Nigdy nie commituj prawdziwego hasła. Do utworzenia konta administratora potrzebne jest hasło 12–128 znaków; konto jest hashowane tą samą metodą co konta zwykłe. Próba użycia BootstrapAdmin w Production przerywa start aplikacji. W Production nie ma automatycznie utworzonego Admina; system nadawania ról należy wdrożyć w kontrolowanym, odrębnym procesie administracyjnym.

## API — odpowiedzi i autoryzacja

| Metoda | Ścieżka | Dostęp | Odpowiedź |
|---|---|---|---|
| `GET` | `/health` | Publiczny | `200` |
| `POST` | `/api/auth/register` | Publiczny | `201` + `Location` / `400` / `409` |
| `POST` | `/api/auth/login` | Publiczny | `200` + JWT / `401` |
| `GET` | `/api/users/me` | JWT | `200` / `401` |
| `GET` | `/api/users` | JWT z rolą Admin | `200` / `401` / `403` |
| `GET` | `/api/users/{id:int}` | JWT z rolą Admin | `200` / `404` / `401` / `403` |
| `DELETE` | `/api/users/{id:int}` | JWT z rolą Admin | `204` / `404` / `409` (samobójcze usunięcie konta) / `401` / `403` |

Zarejestruj:

```http
POST /api/auth/register
Content-Type: application/json

{ "email": "student@example.test", "password": "YourUniqueStrongPassphrase!2026" }
```

Hasło powinno być losowe i niepowtarzane między usługami; powyższy ciąg jest tylko formatem demonstracyjnym. Odpowiedź zawiera wyłącznie `id`, `email`, `role`. `PasswordHash` nie jest serializowany. Dodatkowe pole `role` w żądaniu rejestracji **nie** powoduje awansu konta.

Login:

```http
POST /api/auth/login
Content-Type: application/json

{ "email": "student@example.test", "password": "YourUniqueStrongPassphrase!2026" }
```

Odpowiedź: `{ "accessToken": "...", "tokenType": "Bearer", "expiresAtUtc": "..." }`. Nie kopiuj tokena do GitHuba, ticketów ani logów. Token przesyłaj tylko przez HTTPS w produkcji: `Authorization: Bearer <JWT>`.

### Dlaczego 401 ≠ 403?

- **401 Unauthorized** oznacza brak skutecznego uwierzytelnienia: brak JWT, zły podpis, nieważny issuer/audience, token wygasły, użytkownik usunięty lub błędne hasło przy logowaniu.
- **403 Forbidden** oznacza, że tożsamość jest prawidłowo uwierzytelniona, ale nie ma wymaganych uprawnień. Zwykły użytkownik przy `DELETE /api/users/{id}` otrzymuje właśnie 403.

## Rotacja i bezpieczna konfiguracja

Mechanizm obsługuje klucz bieżący do **podpisywania i weryfikacji** oraz maksymalnie 3 poprzednie klucze wyłącznie do **weryfikacji**:

1. Przechowaj stary klucz w bezpiecznym magazynie sekretów. Wygeneruj nowy losowy klucz (64 bajty).
2. W sekrecie wdrożeniowym ustaw nowy `Jwt__SigningKey` oraz `Jwt__PreviousSigningKeys__0` ze starym kluczem. Zachowaj issuer, audience i maksymalnie 15-minutowy expiry.
3. Wdrażaj w sposób skoordynowany: podczas rolling deploy starsze instancje nie zweryfikują nowych tokenów bez wcześniejszego dodania nowego klucza do ich listy weryfikacyjnej. Najbezpieczniejsza jest aktualizacja dwuetapowa (najpierw klucze weryfikacyjne, potem podpisujący).
4. Po upływie **maksymalnego TTL tokenów + 30 s skew**, kiedy nie ma już starych aktywnych tokenów, usuń stary klucz z listy i magazynu. W razie kompromitacji sekretu pomiń okres przejściowy, natychmiast odwołaj klucz, a istniejące JWT unieważnij.
5. Identyfikator `kid` tokena jest odciskiem SHA-256 klucza, a **nie** samym kluczem. Lista weryfikacyjna jest jawnie ograniczona. Dopuszczony algorytm to tylko HS256.

W środowisku Production ustaw sekret np. przez Kubernetes Secret, Vault, cloud secret manager lub chronione zmienne środowiskowe CI. Użycie zwykłych zmiennych środowiskowych nie gwarantuje tajności przed administratorem hosta. Kluczy nie przekazuj jako argumentów CLI. Nie wprowadzaj do repo prawdziwego klucza ani danych do bootstrappingu administratora. Nie przesyłaj haseł w logach.

## Ograniczenia wersji edukacyjnej

- Konta w `InMemoryUserStore` znikają przy restarcie. Nie stosuj tej implementacji do rzeczywistych produkcyjnych kont i danych. Dla produkcji dodaj trwałą bazę, unikatowy indeks, potwierdzanie e-mail, reset hasła, MFA, audyt, blokady kont i rate limiting, a aplikację publikuj wyłącznie pod HTTPS.
- Po usunięciu konta middleware odrzuca jego uprzednio wystawione tokeny (weryfikacja konta w `OnTokenValidated`), ale nie ma pełnej listy unieważnionych `jti` ani mechanizmu logout.
- `PasswordHasher<T>` ASP.NET Core Identity używa haszowania haseł z solą (domyślnie PBKDF2 v3), a nie szybkiego SHA-256. Każda rejestracja tworzy nowy hash.
- Dla demonstracji i w testach działa jeden proces serwera. Produkcja potrzebuje centralnej trwałej bazy użytkowników współdzielonej przez instancje.

## Testy

Z głównego katalogu repozytorium:

```bash
# Linux/macOS
bash week07/test.sh

# Windows PowerShell
.\week07\test.ps1
```

Testy generują losowy sekret i hasła **w czasie wykonania**. Wykonują 60 scenariuszy HTTP: rejestracja, walidacja, weryfikacja hasła, JWT/claims, 401/403, AdminOnly, usuwanie, odrzucanie podrobionych, wygasłych i nieprawidłowych tokenów oraz rotacja kluczy. Test uruchamia API jako osobny proces i zamyka go po wykonaniu. CI: `.github/workflows/week07-ci.yml`.

**Status:** w środowisku tworzenia paczki nie ma .NET 10 SDK ani dostępu do pobrania go, dlatego nie wykonano kompilacji i testów HTTP. Uruchom je lokalnie lub sprawdź zielony wynik GitHub Actions. Kontrole statyczne i ich wyniki opisano w `TEST_REPORT.md`.

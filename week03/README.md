# WWSI C# — Semestr 5, Week 03: Walidacja modeli i DTO

Wykonano komplet **5 ćwiczeń / 100 pkt według punktacji zadania**, na podstawie oryginalnego [README Week 03](https://github.com/marcinpytlik/WWSI_CSharp/blob/main/semestr5/exercises/week03/README.md). Projekt korzysta z **ASP.NET Core Minimal API, .NET 10** i nie potrzebuje zewnętrznych pakietów NuGet. Nie ma zewnętrznej bazy danych — zapis w pamięci znika po wyłączeniu aplikacji.

## Realizacja wymagań

| Ćwiczenie | Punkty | Implementacja / weryfikacja |
|---|---:|---|
| 1. DTO wejściowe | 10 | `Dto/CreateUserRequest.cs` i mapowanie w `Services/UserRegistrationService.cs` do `Domain/User`; ID i znacznik czasu nie mogą być podane w DTO. W odpowiedzi osobne `UserResponse`. |
| 2. DataAnnotations | 15 | `[Required]`, `[StringLength]`, `[EmailAddress]`, `[Range]`, `[RegularExpression]` z komunikatami po polsku. Żądania z błędami kończą się HTTP 400. |
| 3. Walidacja biznesowa | 20 | `UserRegistrationService` + `InMemoryUserRepository.TryAdd` odrzucają zajęty login niezależnie od wielkości liter. Atomowy lock zapobiega duplikatom przy równoczesnym POST. HTTP 409 + ProblemDetails. |
| 4. ProblemDetails | 25 | `Results.ValidationProblem(errors)` zwraca HTTP 400 `application/problem+json`, `status`, `title`, `errors` zawierające komunikaty według nazw pól JSON. |
| 5. Pipeline walidacji | 30 | `ValidationFilter<TRequest>` + `DataAnnotationsRequestValidator` reużyte zarówno dla `POST /api/users`, jak i `POST /api/tasks`; logika endpointów nie kopiuje adnotacji ani walidacji. |

**Suma punktacji zadań: 100 pkt.** To opis pokrycia wymagań, **nie** oficjalna ocena od prowadzącego.

## Uruchomienie

Wymagane: [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

Z **katalogu głównego repozytorium** (tego, w którym jest `week03/`):

```bash
dotnet run --project week03/src/Week03.Api/Week03.Api.csproj -- --urls=http://localhost:5083
```

Następnie `http://localhost:5083/health` powinno zwracać `{ "status": "healthy" }`. W pliku `src/Week03.Api/Week03.Api.http` znajduje się zestaw żądań do Visual Studio lub rozszerzenia REST Client.

## Endpointy

| Metoda | Adres | Odpowiedź |
|---|---|---|
| GET | `/health` | 200, stan aplikacji |
| POST | `/api/users` | 201, nowy użytkownik; 400 za DTO; 409 za login |
| GET | `/api/users` | 200, tablica użytkowników |
| GET | `/api/users/{id}` | 200 lub 404 |
| POST | `/api/tasks` | 201, nowe zadanie; 400 za DTO |
| GET | `/api/tasks` | 200, tablica zadań |
| GET | `/api/tasks/{id}` | 200 lub 404 |

### Prawidłowy POST /api/users

```http
POST /api/users HTTP/1.1
Content-Type: application/json

{
  "login": "student01",
  "name": "Jan Kowalski",
  "email": "jan@example.com",
  "age": 22
}
```

`201 Created`, nagłówek `Location: /api/users/1` i JSON zawierający wyłącznie `id`, `login`, `name`, `email`, `age`. Klient nie może ustawić `id` w DTO.

### Błędne DTO → HTTP 400 / ValidationProblem

```json
{
  "login": "x",
  "name": "J",
  "email": "nie-email",
  "age": 15
}
```

Odpowiedź ma format `application/problem+json`, `status: 400`, pole `errors` z tablicami komunikatów dla `login`, `name`, `email`, `age`. Błędy są zbierane razem, nie tylko pierwszy.

### Powtórny login → HTTP 409 / ProblemDetails

Przesłanie użytkownika o loginie `STUDENT01` po zapisaniu `student01` zwróci `status: 409` i `code: "duplicate_login"` (bez ponownego dodania użytkownika). To **reguła biznesowa**, odrębna od adnotacji.

### Prawidłowy POST /api/tasks

```json
{
  "title": "Przygotować API",
  "description": "Wspólny pipeline walidacji",
  "priority": 3
}
```

`title` ma 3–120 znaków, opcjonalny `description` maksymalnie 500, `priority` jest wymagany i ma wartość 1–5. Błędy również obsługuje ten sam filtr.

## Uruchamianie testów

Z katalogu głównego repozytorium:

- Windows PowerShell: `./week03/test.ps1`
- Linux/macOS: `./week03/test.sh`

Lub ręcznie:

```bash
python3 week03/static_check.py
dotnet build week03/src/Week03.Api/Week03.Api.csproj -c Release
dotnet build week03/tests/Week03.IntegrationTests/Week03.IntegrationTests.csproj -c Release
dotnet run --project week03/tests/Week03.IntegrationTests/Week03.IntegrationTests.csproj -c Release --no-build
```

`Week03.IntegrationTests` to samodzielny program testowy .NET: uruchamia API na wolnym porcie, wykonuje **60 scenariuszy rzeczywistych żądań HTTP**, zamyka serwer i ustawia kod wyjścia 1 po wykryciu błędu. Bez zależności od xUnit/NuGet. Scenariusze obejmują walidację obu DTO, wielopolowe ValidationProblem, granice parametrów, 400/409/404, brak zapisu przy błędzie, Location, mapowanie DTO, wycieki pól, a także współbieżne tworzenie tego samego loginu.

`.github/workflows/week03-ci.yml` automatycznie buduje i uruchamia testy w GitHub Actions z użyciem .NET 10 po `push` lub `pull_request` ze zmianami w `week03`.

**Status wykonania:** testy statyczne sprawdzono podczas generowania paczki. Środowisko generowania **nie zawiera SDK .NET 10**, dlatego nie wykonano kompilacji ani testów HTTP; muszą zostać uruchomione lokalnie lub przez GitHub Actions. Zobacz `TEST_REPORT.md`.

## Wrzucenie na GitHuba

Rozpakuj ZIP do **katalogu głównego Twojego repozytorium**. Zachowaj istniejące `README.md` i `.gitignore`, scalając zawartość zamiast bezwarunkowego nadpisywania. Skopiuj cały folder `week03` oraz `.github/workflows/week03-ci.yml`.

```bash
git add week03 .github/workflows/week03-ci.yml
git commit -m "Implement week03 DTO, validation and ProblemDetails"
git push
```

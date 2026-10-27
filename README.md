# WWSI — C# / ASP.NET Core (semestr 5)

Repozytorium z ćwiczeniami programistycznymi w C# i ASP.NET Core na platformie **.NET 10**. Każdy tydzień jest osobnym projektem, zawierającym kod aplikacji, testy i dokumentację.

## Zakres

| Katalog | Temat |
|---|---|
| [week01](week01/README.md) | Minimal API |
| [week02](week02/README.md) | Routing i parametry |
| [week03](week03/README.md) | DTO, walidacja i ProblemDetails |
| [week04](week04/README.md) | Entity Framework Core i migracje |
| [week05](week05/README.md) | Operacje CRUD z EF Core |
| [week06](week06/README.md) | Relacje i zapytania EF Core |
| [week07](week07/README.md) | JWT, uwierzytelnianie i autoryzacja |
| [week08](week08/README.md) | Middleware, logowanie i health checks |
| [week09](week09/README.md) | Testy integracyjne API |
| [week10](week10/README.md) | Projekt końcowy — użytkownicy, zadania i tagi |

## Wymagania

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Dostęp do NuGet podczas pierwszego przywracania zależności
- Python 3 oraz Bash dla skryptów testowych (`test.sh`); na Windows można korzystać z PowerShell (`test.ps1`)

## Uruchomienie

W katalogu głównym repozytorium:

```bash
# Przykład — pierwsza aplikacja
dotnet run --project week01/src/Week01.Api/Week01.Api.csproj

# Testy wybranego tygodnia (Linux/macOS/WSL)
bash week01/test.sh
```

Na Windows w PowerShell:

```powershell
.\week01\test.ps1
```

Analogicznie można uruchomić `week02`–`week10`. Szczegółowe endpointy, wymagane ustawienia środowiskowe i polecenia EF Core opisano w plikach `weekXX/README.md`.

## Automatyczne testy

Wspólny workflow [CI](.github/workflows/ci.yml) automatycznie wykrywa katalogi `weekXX` i uruchamia osobny zestaw testów każdego dostępnego tygodnia przy `push` i `pull_request`. Można go również wywołać ręcznie z zakładki **Actions** w GitHubie.

Wynik kompilacji i testów należy sprawdzić w zakładce **Actions**; obecność kodu i skryptów testowych nie jest potwierdzeniem, że testy przeszły.

## Organizacja repozytorium

```text
.github/
  workflows/
    ci.yml
.gitignore
README.md
week01/
week02/
...
week10/
```

Zestaw `.github/`, `.gitignore` i główny `README.md` powinien znajdować się **tylko raz, w katalogu głównym**. W katalogach `weekXX` pozostają ich projekty i własne instrukcje.

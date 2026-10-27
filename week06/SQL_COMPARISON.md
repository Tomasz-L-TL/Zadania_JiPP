# Week 06 — Include vs Select — analiza zapytań

## Co jest porównywane?

W klasie `Queries/UserQueries.cs` zdefiniowano obok siebie:

1. `FullGraph`: `AsNoTracking()`, dwie ścieżki `.Include(...).ThenInclude(...)`, `.AsSplitQuery()`.
2. `Summary`: `AsNoTracking()`, projekcja `.Select(new UserSummaryResponse(...))`, agregacje `Count`, bez `Include`.

`GET /api/users/{id}/graph` materializuje kompletny **User → Tasks → Comments i Tags** i mapuje wynik na DTO.
`GET /api/users/{id}/summary` pobiera wyłącznie pola potrzebne do podsumowania: `User.Id`, `Name`, listę `Task.Id`, `Title`, `CommentCount`, `TagCount` i sumy zadań.

## Jak wygenerować faktyczny SQL z danego środowiska

Uruchom w katalogu głównym:

```bash
bash week06/test.sh
```

Scenariusze **38 i 39** wydrukują SQL za pomocą `ToQueryString()`:

```csharp
Console.WriteLine(UserQueries.FullGraph(db, 1).ToQueryString());
Console.WriteLine(UserQueries.Summary(db, 1).ToQueryString());
```

**Uwaga:** w przypadku `AsSplitQuery()` metoda `ToQueryString()` pokazuje zapytanie dla pierwszej części grafu i informuje, że dodatkowe części są osobnymi zapytaniami. Nie traktuj tego wydruku jako kompletnego logu. Aby obejrzeć *wszystkie* polecenia SQL rzeczywiście wysłane do bazy, ustaw w Development:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

Uruchom API i odpytaj obie trasy, a następnie porównaj w konsoli `Executed DbCommand` wraz z parametrami czasu. Nie należy włączać `EnableSensitiveDataLogging()` na produkcji, ponieważ może to ujawniać dane w logach.

## Różnice w zapytaniach (oczekiwane na podstawie definicji LINQ)

| Kryterium | FullGraph / Include | Summary / projection |
|---|---|---|
| Dane wczytywane | Encje `User`, `TaskItem`, `Comment`, `Tag` | Wybrane pola DTO i agregaty SQL |
| Materializacja relacji | Tak, z kolekcjami i nawigacjami | Nie, tylko DTO i wyniki agregacji |
| Rodzaj SQL | Odczyt powiązanych tabel, `AsSplitQuery` dzieli kolekcje | Projekcja / `COUNT(*)` w SQL, zależnie od translacji EF |
| Liczba zapytań | Kilka dla rozdzielonych kolekcji | Zależy od translacji zagnieżdżonej kolekcji; nie zakładaj z góry jednego |
| Tracking | Wyłączony przez `AsNoTracking()` | Wyłączony; w DTO nie ma encji |
| Przesył danych | Więcej danych, pełna treść komentarzy i nazwy tagów | Mniej danych, brak treści komentarzy i tagów |
| Zalecane zastosowanie | Szczegóły / edytor pełnego grafu, ekran z tagami i komentarzami | Lista / dashboard / paginowany przegląd, gdy potrzebne są liczniki |

## Jak interpretować wyniki

Dla `Include` dwóch niezależnych kolekcji w jednym złączeniu (`Comments` i `Tags`) dochodzi do mnożenia wierszy (*cartesian explosion*). `AsSplitQuery` ogranicza ten problem, lecz generuje dodatkowe round-trip’y. Złożona projekcja jest zwykle korzystniejsza dla odczytów listowych, ponieważ pobiera mniej pól. Nie oznacza to, że zawsze wystarczy jedno polecenie SQL lub że zawsze będzie szybsza — zależy to od dostawcy bazy, indeksów, wielkości danych i planu zapytania.

Wydajność w konkretnym systemie weryfikuj na reprezentatywnych danych, z realnymi czasami w logach i analizą `EXPLAIN QUERY PLAN` SQLite. Projekt dostarcza **analizę techniczną i kod generujący SQL**, nie deklaruje wyników benchmarku bez uruchomienia testów.

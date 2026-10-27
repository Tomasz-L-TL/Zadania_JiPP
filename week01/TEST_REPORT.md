# Raport weryfikacji pakietu week01

**Kontrole statyczne:** 21/21 zaliczone.

**Testy integracyjne HTTP (.NET 10):** dostarczono 18 scenariuszy, ale nie uruchomiono ich w tym środowisku, ponieważ brakuje SDK .NET 10. Wynik wykonania musi zostać potwierdzony przez GitHub Actions lub lokalne `dotnet run`.

## Zakres sprawdzeń statycznych

- [x] Oba pliki csproj są poprawnym XML
- [x] Oba projekty celują w net10.0
- [x] appsettings.json jest poprawnym JSON
- [x] appsettings.json nie zawiera kluczy sekretów
- [x] Hello API: ścieżka /hello i JSON
- [x] Health check: UTC i assembly version
- [x] Konfiguracja: DTO i IOptions
- [x] Wersjonowanie: MapGroup /api/v1
- [x] GET collection: /notes
- [x] GET note by GUID
- [x] POST note and 201
- [x] DELETE note and 204/404
- [x] Walidacja: puste pola i limity
- [x] Store jest współbieżny
- [x] Testy uruchamiają HTTP API
- [x] 18 unikatowych scenariuszy HTTP
- [x] Testy wstrzykują i kontrolują sekret
- [x] Dobór konfiguracji testowanego API Debug/Release
- [x] CI ma poprawny YAML
- [x] CI używa .NET 10 i wykonuje testy
- [x] Skrypty testowe Windows i Linux

## Uruchomienie testów rzeczywistych

```powershell
cd week01
.\test.ps1
```

Lub w zakładce GitHub Actions: workflow `Week 01 - Build and HTTP integration tests`.

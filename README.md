# Medical Booking System

System rezerwacji wizyt medycznych z API ASP.NET Core, aplikacją pacjenta w Blazor WebAssembly i panelem obsługi w Blazor Server. Dane przechowywane są w SQLite przez Entity Framework Core.

Projekt jest rozwijany etapami. Pierwszy etap dopracowania obejmuje .NET 10 LTS, przenośną konfigurację, migracje i automatyczne testy regresyjne. Pełna kontrola dostępu, atomowe rezerwacje i cykl życia wizyty są kolejnymi zadaniami opisanymi w [planie prac](docs/ROADMAP.md). Obecną wersję uruchamiaj lokalnie na fikcyjnych danych; etap zabezpieczeń nie jest jeszcze zakończony.

## Wymagania

- .NET SDK **10.0.113** — wersja przypięta w `global.json`; obsługiwane są nowsze poprawki tej samej serii `10.0.1xx`.
- Windows, Linux albo macOS z obsługą .NET 10.
- Opcjonalnie Visual Studio lub Rider; aplikacje można też uruchomić z terminala.

Wersje pakietów znajdują się w `Directory.Packages.props`. Znane podatności zależności powodują błąd przywracania pakietów w CI.

## Szybki start

Z katalogu głównego repozytorium:

```bash
dotnet restore MedicalBookingSystem/MedicalBookingSystem.sln
dotnet build MedicalBookingSystem/MedicalBookingSystem.sln --configuration Release --no-restore
dotnet test MedicalBookingSystem/MedicalBookingSystem.sln --configuration Release --no-build --no-restore
```

Uruchom API jako pierwsze:

```bash
dotnet run --project MedicalBookingSystem/MedicalBookingSystem.WebAPI
```

W profilu Development API automatycznie nakłada migracje na bazę `MedicalBookingSystem/data/medical.db` i przygotowuje fikcyjne dane demo. Baza nie jest przechowywana w Git.

W dwóch kolejnych terminalach uruchom aplikacje:

```bash
dotnet run --project MedicalBookingSystem/MedicalBookingSystem.BlazorWasm
```

```bash
dotnet run --project MedicalBookingSystem/MedicalBookingSystem.BlazorServer
```

| Aplikacja | Adres lokalny |
| --- | --- |
| Aplikacja pacjenta | http://localhost:5016 |
| API i Swagger | http://localhost:5031/swagger |
| Panel obsługi | http://localhost:5187/admin/doctors |
| Kontrola działania API | http://localhost:5031/health/live |

Konto pacjenta demo: `patient@example.test`, hasło `DemoPatient2026!`. To publiczne dane demonstracyjne, dostępne wyłącznie po włączeniu seeda. Lekarz, pacjent i wizyty są fikcyjne; przyszłe grafiki są tworzone względem daty pierwszego uruchomienia. Jeżeli istniejąca baza demo z czasem przestanie zawierać przyszłe terminy, dodaj grafik przez panel lub utwórz nową, pustą bazę demo pod inną ścieżką.

## Konfiguracja

Wspólna ścieżka bazy ustawiana jest przez `ConnectionStrings:MedicalBookingSystem`. Ścieżki względne rozwiązywane są względem katalogu z plikiem rozwiązania, a po publikacji — względem katalogu aplikacji. API i panel muszą wskazywać tę samą bazę.

Ustawienia można nadpisać przez zmienne środowiskowe .NET, np.:

- `ConnectionStrings__MedicalBookingSystem` — pełny connection string SQLite.
- `Clinic__TimeZone` — strefa placówki; domyślnie `Europe/Warsaw`.
- `Database__InitializeOnStartup` — nakładanie migracji przy starcie API; domyślnie wyłączone poza Development.
- `Seed__Enabled` i `Seed__PatientPassword` — opcjonalne fikcyjne dane demonstracyjne.
- `JwtSettings__SecretKey` — co najmniej 32 bajty; wymagany poza Development.
- `Cors__AllowedOrigins__0` — dozwolony adres aplikacji pacjenta.
- `AllowedHosts` — nazwy hostów obsługiwane przez wdrożenie.

Development generuje tymczasowy klucz JWT na czas życia procesu. Restart API unieważnia takie tokeny. Klucz używany poza środowiskiem lokalnym przechowuj w konfiguracji środowiska, bez wpisywania go do repozytorium.

Daty wizyt i grafików oznaczają lokalny czas placówki. Znaczniki utworzenia dokumentów oraz terminy wygaśnięcia tokenów korzystają z UTC. Reguły wymagające bieżącego czasu używają kontrolowanego `ClinicClock` opartego na `TimeProvider`.

## Migracje

```bash
dotnet tool restore
dotnet ef database update --project MedicalBookingSystem/MedicalBookingSystem.Infrastructure --startup-project MedicalBookingSystem/MedicalBookingSystem.WebAPI
```

Fabryka czasu projektowania nie uruchamia hostów ani seeda. Domyślnie narzędzie aktualizuje ten sam plik `MedicalBookingSystem/data/medical.db`, którego używają aplikacje. Dla innej bazy ustaw `MEDICAL_MIGRATION_CONNECTION` na właściwy connection string przed wykonaniem polecenia. W Development szybki start nakłada migracje automatycznie.

Początkowa migracja służy do tworzenia nowej bazy. Stara baza utworzona przez `EnsureCreated` nie ma historii migracji i nie powinna być aktualizowana tym poleceniem bez osobnego planu przeniesienia danych.

## Struktura i testy

| Projekt | Odpowiedzialność |
| --- | --- |
| Domain | Encje i kontrakty repozytoriów |
| Application | Usługi przypadków użycia, walidatory, jawne mapowania DTO i zegar |
| Infrastructure | EF Core, SQLite, repozytoria, Unit of Work i migracje |
| SharedKernel | Kontrakty DTO i wyniki operacji |
| WebAPI | HTTP, konfiguracja JWT i obsługa błędów |
| BlazorWasm | Interfejs pacjenta |
| BlazorServer | Panel obsługi |

Testy jednostkowe korzystają z xUnit i Moq, integracyjne z rzeczywistego API i SQLite, a komponentowe z bUnit. Workflow `Build and test` sprawdza rozwiązanie na Linux i Windows oraz zapisuje raporty TRX i Cobertura.

Szczegóły: [strategia testowania](docs/TESTING.md) i [plan dalszych etapów](docs/ROADMAP.md).

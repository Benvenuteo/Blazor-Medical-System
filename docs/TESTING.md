# Strategia testowania

Testy chronią reguły biznesowe, kontrakty HTTP i zachowanie bazy danych. Testy jednostkowe nie wymagają pliku SQLite ani uruchomionego serwera. Testy integracyjne uruchamiają rzeczywistą aplikację przez `WebApplicationFactory` i korzystają z osobnej bazy SQLite w pamięci dla każdego przypadku.

## Uruchomienie

Polecenia wykonuj z katalogu głównego repozytorium:

```bash
dotnet restore MedicalBookingSystem/MedicalBookingSystem.sln
dotnet build MedicalBookingSystem/MedicalBookingSystem.sln --configuration Release --no-restore
dotnet test MedicalBookingSystem/MedicalBookingSystem.sln --configuration Release --no-build --no-restore
```

Raporty TRX i pokrycie kodu:

```bash
dotnet test MedicalBookingSystem/MedicalBookingSystem.sln --configuration Release --no-build --logger trx --results-directory TestResults --collect "XPlat Code Coverage" --settings coverage.runsettings
```

Raporty są również publikowane jako artefakty workflow `Build and test`. Pokrycie jest informacją pomocniczą; nie zastępuje sprawdzenia poprawności asercji ani testów przypadków negatywnych.

## Podział

| Projekt | Co sprawdza | Izolacja |
| --- | --- | --- |
| `MedicalBookingSystem.UnitTests` | Hasła, anulowanie wizyt, walidacja grafiku, granice czasu i wartości, mapowanie DTO | xUnit, Moq, kontrolowany zegar |
| `MedicalBookingSystem.IntegrationTests` | Rzeczywiste HTTP, rejestracja i logowanie, migracje, unikalność i klucze obce, ładowanie relacji EF | WebApplicationFactory i SQLite |
| `MedicalBookingSystem.ComponentTests` | Reakcja komponentu Blazor na nieudaną lub pustą odpowiedź z opiniami | bUnit i kontrolowany HttpMessageHandler |

## Reguły projektowania testów

- Każdy przypadek ma układ Arrange–Act–Assert i sprawdza obserwowalne zachowanie.
- Daty testowe pochodzą z `TestTimeProvider`, a nie z bieżącego czasu komputera.
- Granica anulowania i rezerwacji jest włączająca: dokładnie 24 godziny jest dozwolone.
- Przedziały grafików mogą stykać się końcami; nakładające się przedziały są odrzucane.
- Ukończone 18 lat spełnia warunek wieku; testowane są również dni bezpośrednio przed i po granicy.
- Odrzucona zmiana hasła nie może zmieniać śledzonej encji ani wywoływać zapisu.
- Testy asynchroniczne sterują zakończeniem zadań przez `TaskCompletionSource`; nie używają opóźnień do ustalania kolejności.
- Testy repozytoriów czyszczą tracker EF przed odczytem, aby stan przygotowany w Arrange nie ukrywał brakujących `Include`.
- Dane testowe są fikcyjne. Testy nie korzystają z roboczej bazy aplikacji.

## Kolejne pakiety testów

Plan dalszej pracy znajduje się w [ROADMAP.md](ROADMAP.md). Następny pakiet obejmuje odmowę dostępu bez tokenu, role użytkowników i dostęp do cudzych zasobów. Testy współbieżności rezerwacji będą używały oddzielnych kontekstów i połączeń do tej samej tymczasowej bazy plikowej; pojedyncze połączenie SQLite w pamięci nie odtwarza tego scenariusza.

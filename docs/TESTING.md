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
| `MedicalBookingSystem.UnitTests` | Hasła, anulowanie wizyt, walidacja grafiku, granice czasu i wartości, mapowanie DTO, uprawnienia usług, sloty, statusy wizyty i zmiany czasu | xUnit, Moq, kontrolowany zegar |
| `MedicalBookingSystem.IntegrationTests` | Rzeczywiste JWT i cookie, odmowa dostępu, własność zasobów, CSRF, limity logowania, migracje, ograniczenia SQLite, równoczesne zapisy i pełny przebieg wizyty | WebApplicationFactory i SQLite |
| `MedicalBookingSystem.ComponentTests` | Obsługa opinii, zapis i usuwanie tokenu, wygasła sesja, nagłówek Bearer, reakcja na 401 oraz edycja/rezerwacja i odświeżenie slotów po 409 | bUnit i kontrolowany HttpMessageHandler |

## Reguły projektowania testów

- Każdy przypadek ma układ Arrange–Act–Assert i sprawdza obserwowalne zachowanie.
- Testy granic reguł biznesowych korzystają z `TestTimeProvider`. Testy rzeczywistych podpisanych JWT używają krótkich okresów ważności względem zegara hosta.
- Granica anulowania i rezerwacji jest włączająca: dokładnie 24 godziny jest dozwolone.
- Przedziały grafików mogą stykać się końcami; nakładające się przedziały są odrzucane.
- Ukończone 18 lat spełnia warunek wieku; testowane są również dni bezpośrednio przed i po granicy.
- Odrzucona zmiana hasła nie może zmieniać śledzonej encji ani wywoływać zapisu.
- Testy asynchroniczne sterują zakończeniem zadań przez `TaskCompletionSource`; nie używają opóźnień do ustalania kolejności.
- Testy repozytoriów czyszczą tracker EF przed odczytem, aby stan przygotowany w Arrange nie ukrywał brakujących `Include`.
- Testy odmowy zapisu sprawdzają również niezmieniony stan w bazie; sam status 403 nie wystarcza.
- Testy HTTP używają prawdziwego podpisywania i walidacji JWT oraz rzeczywistego cookie, bez zastępowania mechanizmu uwierzytelniania atrapą.
- Dane testowe są fikcyjne. Testy nie korzystają z roboczej bazy aplikacji.

## Kolejne pakiety testów

Plan dalszej pracy znajduje się w [ROADMAP.md](ROADMAP.md). Zestaw obejmuje 221 przypadków .NET: 96 jednostkowych, 116 integracyjnych i 9 komponentowych, oraz cztery scenariusze E2E. Dokładne wyniki każdego przebiegu zapisują raporty TRX i raport Playwright. Plan dalszej pracy obejmuje zarządzanie kontami, unieważnianie sesji, audyt dokumentacji i stronicowanie pozostałych list.

## Współbieżność i czas

- Testy równoczesnych rezerwacji używają osobnych połączeń i kontekstów do tej samej tymczasowej bazy plikowej SQLite. Pojedyncze połączenie do bazy w pamięci nie odtwarza tego scenariusza.
- `ConcurrentSaveGate` zatrzymuje oba żądania po sprawdzeniu dostępności, przed zapisem. Testy wymuszają zatem wyścig zapisu, bez opóźnień typu `Sleep` i zależności od kolejności wątków.
- Sprawdzany jest jeden zwycięzca i odpowiedź 409 dla drugiego żądania, kolizja dwóch lekarzy w kalendarzu pacjenta oraz zachowanie pierwotnej wizyty po przegranej zmianie terminu.
- Test optymistycznej współbieżności ładuje tę samą wizytę do dwóch kontekstów i potwierdza, że spóźniony zapis nie nadpisuje zatwierdzonego anulowania.
- Test pełnego przebiegu używa rzeczywistego HTTP: rejestracja, rezerwacja, odmowa przedwczesnego zakończenia/dokumentacji/opinii, zakończenie przez lekarza, dokumentacja, opinia i historia pacjenta. Przesuwany jest wyłącznie kontrolowany zegar. Daty i statusy zapisanej wizyty nie są ręcznie zmieniane w bazie.
- W tym jednym scenariuszu walidator czasu JWT otrzymuje ten sam zegar testowy. Podpis, algorytm, wystawca, odbiorca i uprawnienia pozostają sprawdzane przez rzeczywisty handler. Pozostałe testy bezpieczeństwa korzystają z domyślnej walidacji czasu tokenów.
- Osobne przypadki pokrywają obie zmiany czasu w `Europe/Warsaw`, pełne 24 godziny czasu rzeczywistego oraz granice grafiku i 3-miesięcznego horyzontu.

## Przeglądarka i wydajność

Do testów E2E potrzebny jest Node.js 24 oraz Chromium instalowany przez przypiętą wersję Playwright:

```bash
npm ci --prefix tests/e2e
cd tests/e2e
npx playwright install chromium
npm test
```

Przed testem aplikacje muszą być zbudowane w konfiguracji Release. Porty 5016, 5031 i 5187 powinny być wolne. Skrypt uruchamia trzy prawdziwe hosty, świeżą bazę i osobny katalog zdjęć w katalogu tymczasowym. Nie korzysta z roboczej bazy ani z już uruchomionych serwerów. Wybranie `CHROMIUM_EXECUTABLE_PATH` pozwala lokalnie wskazać własny zgodny plik przeglądarki; CI używa instalacji Playwright.

Scenariusze: rejestracja z walidacją wieku; pacjent wyszukuje, rezerwuje, przenosi i anuluje wizytę oraz wylogowuje się; lekarz kończy wizytę i tworzy dokumentację, którą pacjent odczytuje, a następnie dodaje opinię; administrator odrzuca fałszywe zdjęcie, publikuje poprawne i zapisuje grafik. Testy korzystają z rzeczywistych formularzy, cookie, JWT, SignalR i HTTP, bez przechwytywania odpowiedzi API.

Raport HTML, ślady błędów i zrzuty ekranów znajdują się w `TestResults/e2e/`. W CI publikuje je zadanie `Browser workflows / Ubuntu`. Zrzuty zawierają wyłącznie fikcyjne dane demonstracyjne. Testy czekają na obserwowalny stan interfejsu, zamiast używać `Sleep`.

`DirectoryScaleTests` przygotowuje 100 lekarzy, 1400 grafików, 1000 historycznych wizyt i 1000 opinii. Sprawdza stronicowanie w SQL, dwie komendy SELECT i brak śledzenia grafów. Zapisuje diagnostyczne próbki czasów nowego odczytu i odtworzonego poprzedniego zapytania. Do wyeksportowania JSON ustaw `MEDICAL_BENCHMARK_OUTPUT` na ścieżkę pliku i uruchom:

```bash
dotnet test MedicalBookingSystem/MedicalBookingSystem.IntegrationTests --configuration Release --no-build --filter Category=Performance --logger trx --results-directory TestResults/benchmark
```

Czas nie ma sztywnego progu w CI. Porównanie używa lokalnej bazy plikowej i nie stanowi testu obciążeniowego publicznego wdrożenia. Metodyka i zapisany wynik: [PERFORMANCE.md](PERFORMANCE.md).

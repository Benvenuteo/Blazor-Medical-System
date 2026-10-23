# Plan dopracowania projektu

Zakres wersji portfolio jest ukończony i opisany poniżej. Projekt prezentuje działające przypadki użycia, regresje, architekturę i obsługę awarii na danych demonstracyjnych. Publiczne wdrożenie z rzeczywistymi danymi jest osobnym przedsięwzięciem organizacyjnym.

## 1. Uruchamianie i podstawa testów

- [x] .NET 10 LTS i przypięta wersja SDK.
- [x] Centralne wersje pakietów, usunięte nieużywane zależności i podatna wersja AutoMappera.
- [x] Jawne mapowania DTO, bez ujawniania hashy haseł.
- [x] Przenośna konfiguracja SQLite i początkowa migracja EF.
- [x] Fikcyjne dane demo z aktualnymi terminami i prawdziwym hashowaniem haseł.
- [x] Kontrolowany zegar: grafiki i wizyty w strefie placówki, znaczniki audytowe w UTC.
- [x] Testy jednostkowe, integracyjne i komponentowe.
- [x] CI na Linux i Windows z raportami testów i pokrycia.
- [x] Regresje: weryfikacja aktualnego hasła przed zmianą, oczekiwanie na walidację grafiku, granice 24 godzin i wieku, sortowanie bez regionu, obsługa błędu opinii.
- [x] Usunięte z indeksu pliki IDE oraz robocza baza i pliki WAL/SHM.

## 2. Uwierzytelnianie i uprawnienia

- [x] Wymagane uwierzytelnienie dla wszystkich prywatnych operacji API.
- [x] Identyfikator pacjenta i lekarza pobierany ze zweryfikowanej tożsamości.
- [x] Konta i role pacjenta, lekarza oraz administratora; zabezpieczony panel Server.
- [x] Wysyłanie tokenu przez WASM, poprawne wylogowanie i obsługa wygaśnięcia sesji.
- [x] Kontrola własności wizyt i dokumentów również w warstwie aplikacyjnej.
- [x] Testy żądań anonimowych, niewłaściwej roli i dostępu do cudzych danych.

## 3. Rezerwacje i cykl życia wizyty

- [x] Jednoznaczny model długości wizyty i dostępnych slotów.
- [x] Poprawne wyszukiwanie po dacie i najbliższym wolnym terminie.
- [x] Atomowa rezerwacja i ochrona przed równoczesnym zajęciem tego samego terminu.
- [x] Oznaczanie wizyt jako odbytych i kontrola dozwolonych przejść statusów.
- [x] Bezpieczna zmiana terminu, z wykluczeniem własnej wizyty z konfliktów.
- [x] Wspólna walidacja dla API i bezpośrednich wywołań z panelu Server.
- [x] Testy współbieżności i pełnego scenariusza wizyta–dokumentacja–opinia.

## 4. API, wydajność i prezentacja

- [x] Spójne DTO odpowiedzi i ProblemDetails dla błędów.
- [x] Paginacja, filtrowanie, oceny i najbliższa dostępność katalogu lekarzy wykonywane w bazie.
- [x] Kontekst EF tworzony i zwalniany dla każdej operacji w Blazor Server.
- [x] Usunięcie kopii DTO z komponentów WASM.
- [x] Edytor wczytuje bieżącą wizytę i dostępne sloty; konflikt odświeża terminy i dane wizyty.
- [x] Walidacja i ponowne kodowanie zdjęć, sprzątanie plików i publiczny adres mediów w konfiguracji.
- [x] Testy E2E w Chromium, odtwarzalny pomiar wydajności i zrzuty ekranów do portfolio.
- [x] Formularze personelu czekają na gotowość połączenia Blazor, aby nie gubić wczesnego wprowadzania danych.

## 5. Konta personelu i unieważnianie sesji

- [x] Kontrola aktualnego znacznika sesji przy żądaniach API, odczycie cookie i każdej chronionej operacji aplikacyjnej.
- [x] Paginowane zarządzanie kontami personelu, aktywacja i dezaktywacja, unikalny email i przypisanie lekarza.
- [x] Hasła tymczasowe i wymagana zmiana hasła po utworzeniu lub resecie konta.
- [x] Zmiana własnego hasła i wylogowanie wszystkich sesji pacjenta oraz personelu.
- [x] Optymistyczna współbieżność kont i blokada wyłączenia ostatniego administratora w SQLite.
- [x] Testy przechwyconych cookies, starych tokenów, migracji istniejących kont i otwartego panelu po odebraniu dostępu.

## 6. Domknięcie wersji portfolio

- [x] Stronicowanie historii i kolejek wizyt, dokumentów oraz opinii, z filtrowaniem kolejki lekarza przed paginacją.
- [x] Indeksy dla właściciela/statusu/daty i test planu zapytania historii.
- [x] Audyt odczytów oraz utworzenia dokumentacji, atomowy zapis i odmowa wydania treści przy awarii audytu.
- [x] Stronicowany podgląd metadanych audytu dla administratora i ochrona wpisów przed edycją/usunięciem.
- [x] Kopie SQLite Online Backup API z WAL, wskazanymi zdjęciami, sumami kontrolnymi i testowanym odtwarzaniem.
- [x] Bezpieczne odtwarzanie do nowego katalogu, bez reaktywacji starych sesji.
- [x] Narzędzie utworzenia/odzyskania pierwszego administratora bez hasła w argumentach.
- [x] Nagłówki bezpieczeństwa panelu, trwałe klucze Data Protection szyfrowane certyfikatem i jawne zaufane proxy.
- [x] Responsywna nawigacja pacjenta, dostęp klawiaturą do przewijanych tabel i kontrole axe na wybranych ekranach.
- [x] Testy desktop Chromium, widoku mobilnego i Firefox.
- [x] Instrukcja architektury, utrzymania, demo i przykład Nginx.

## Poza zakresem wersji portfolio

Praca z rzeczywistymi danymi medycznymi wymaga decyzji właściciela systemu: modelu tożsamości/BFF zamiast localStorage, retencji i niezależnego magazynu audytu, polityk organizacyjnych, kopii poza serwerem, monitoringu, pełnego audytu manualnego dostępności i bezpieczeństwa oraz przeglądu wymagań prawnych. Projekt nie deklaruje takiego wdrożenia ani zgodności na podstawie samych testów. Harmonogramy kopii, infrastruktura i zewnętrzne usługi nie są automatycznie uruchamiane.

## Kryteria ukończenia

- Świeży klon uruchamia się zgodnie z README na Windows i Linux.
- Kontrola dostępu nie opiera się na identyfikatorze przesłanym przez przeglądarkę.
- Testy chronią wszystkie krytyczne reguły i przypadki negatywne wskazane powyżej.
- Pełny scenariusz pacjent–lekarz działa bez ręcznej edycji bazy.
- CI przechodzi, a opis projektu odpowiada rzeczywiście wdrożonym funkcjom.

# Plan dopracowania projektu

Każdy etap powinien kończyć się działającym buildem, testami regresyjnymi i opisem zmian w pull requeście. Obecny etap przygotowuje podstawę do kolejnych zmian; nie oznacza zakończenia audytu ani gotowości systemu do publicznego wdrożenia.

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

## Kolejne prace

- [ ] Stronicowanie historii wizyt, dokumentów i opinii oraz filtrów pracy lekarza.
- [ ] Testy innych przeglądarek, widoków mobilnych i dostępności interfejsu.

## Dalsze zabezpieczenia przed publicznym wdrożeniem

- [ ] Audyt odczytów i zmian dokumentacji oraz testy odzyskiwania danych z kopii zapasowej.
- [ ] Docelowy przepływ sesji przeglądarkowej, polityka CSP, trwałe klucze Data Protection i konfiguracja zaufanego reverse proxy.

## Kryteria ukończenia

- Świeży klon uruchamia się zgodnie z README na Windows i Linux.
- Kontrola dostępu nie opiera się na identyfikatorze przesłanym przez przeglądarkę.
- Testy chronią wszystkie krytyczne reguły i przypadki negatywne wskazane powyżej.
- Pełny scenariusz pacjent–lekarz działa bez ręcznej edycji bazy.
- CI przechodzi, a opis projektu odpowiada rzeczywiście wdrożonym funkcjom.

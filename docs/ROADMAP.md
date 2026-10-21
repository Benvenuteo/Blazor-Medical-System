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

- [ ] Jednoznaczny model długości wizyty i dostępnych slotów.
- [ ] Poprawne wyszukiwanie po dacie i najbliższym wolnym terminie.
- [ ] Atomowa rezerwacja i ochrona przed równoczesnym zajęciem tego samego terminu.
- [ ] Oznaczanie wizyt jako odbytych i kontrola dozwolonych przejść statusów.
- [ ] Bezpieczna zmiana terminu, z wykluczeniem własnej wizyty z konfliktów.
- [ ] Wspólna walidacja dla API i bezpośrednich wywołań z panelu Server.
- [ ] Testy współbieżności i pełnego scenariusza wizyta–dokumentacja–opinia.

## 4. API, wydajność i prezentacja

- [ ] Spójne DTO odpowiedzi i ProblemDetails dla błędów.
- [ ] Paginacja, filtrowanie i agregacje wykonywane w bazie.
- [ ] Kontekst EF o czasie życia dostosowanym do operacji w Blazor Server.
- [ ] Usunięcie kopii DTO z komponentów WASM.
- [ ] Odczyt bieżących danych podczas edycji wizyty i poprawne odświeżanie list.
- [ ] Walidacja zawartości zdjęć, sprzątanie plików i publiczny adres mediów w konfiguracji.
- [ ] Testy E2E w przeglądarce, pomiary wydajności i zrzuty ekranów do portfolio.

## Dalsze zabezpieczenia przed publicznym wdrożeniem

- [ ] Zarządzanie kontami personelu, reset hasła i wymuszone unieważnianie istniejących sesji po zmianie hasła lub dezaktywacji konta.
- [ ] Audyt odczytów i zmian dokumentacji oraz testy odzyskiwania danych z kopii zapasowej.
- [ ] Docelowy przepływ sesji przeglądarkowej, polityka CSP, trwałe klucze Data Protection i konfiguracja zaufanego reverse proxy.

## Kryteria ukończenia

- Świeży klon uruchamia się zgodnie z README na Windows i Linux.
- Kontrola dostępu nie opiera się na identyfikatorze przesłanym przez przeglądarkę.
- Testy chronią wszystkie krytyczne reguły i przypadki negatywne wskazane powyżej.
- Pełny scenariusz pacjent–lekarz działa bez ręcznej edycji bazy.
- CI przechodzi, a opis projektu odpowiada rzeczywiście wdrożonym funkcjom.

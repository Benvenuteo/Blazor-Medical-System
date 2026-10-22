# Kontrakty HTTP i odczyty katalogu

API zwraca DTO bez koperty `Result`. Typ `Result` pozostaje wewnętrznym wynikiem warstwy aplikacyjnej. Operacje utworzenia wizyty, opinii i dokumentu zwracają `201` oraz DTO i adres zasobu. Rejestracja i logowanie zwracają `200` z `AuthenticationResponseDto` (`token`, `patient`), a logowanie personelu — `TokenResponseDto`. Zmiana profilu, przeniesienie, anulowanie i zakończenie wizyty oraz usunięcie opinii zwracają `204`.

Błędy mają format `application/problem+json`, z polami `status`, `title`, `instance`, `traceId` i opcjonalnym `detail` lub `errors`. Obejmuje to walidację modelu, reguły FluentValidation, 401/403, nieznalezione zasoby, konflikty i nieoczekiwane awarie. Szczegóły błędów SQLite i stos wyjątku trafiają do logu serwera. `traceId` pozwala powiązać odpowiedź z logiem. Nieoczekiwana awaria przechowywania danych zwraca 500; nie jest zamieniana na błąd walidacji.

| Kod | Znaczenie |
| --- | --- |
| 400 | Nieprawidłowy model lub reguła walidacji |
| 401 | Brak ważnego uwierzytelnienia lub błędne dane logowania |
| 403 | Niewłaściwa rola lub własność zasobu |
| 404 | Zasób nie istnieje |
| 409 | Termin/status/unikalny wpis koliduje z bieżącym stanem |
| 429 | Przekroczony limit prób uwierzytelnienia |
| 500 | Nieoczekiwany błąd serwera |

## Wyszukiwanie lekarzy

```http
GET /api/doctors?Region=Małopolskie&SortBy=availability&Page=1&PageSize=10
```

Opcjonalne filtry: `SpecializationId`, `Region`, `Date` w postaci lokalnego dnia `YYYY-MM-DD`. `SortBy` przyjmuje `rating` lub `availability`; bez sortowania obowiązuje rosnący identyfikator. Domyślnie `Page=1`, `PageSize=10`; rozmiar strony wynosi od 1 do 50, numer strony od 1 do 100000. Kolejność ma dodatkowy klucz `Id`, aby remisy nie zmieniały składu stron.

```json
{
  "items": [],
  "totalCount": 0,
  "page": 1,
  "pageSize": 10,
  "totalPages": 0
}
```

Strona poza zakresem zawiera pustą listę i prawdziwą liczbę wyników. Usunięto nieograniczony endpoint `/api/doctors/all`; wybór lekarza w rezerwacji i panel administratora używają tego samego kontraktu stron. Przy przejściu z wyszukiwarki do rezerwacji `doctorId` wskazuje wybranego lekarza, którego dane są pobierane osobno.

Filtrowanie, średnia/liczba ocen, najbliższy wolny termin, kolejność i paginacja wykonywane są w SQLite. Zapytanie odczytuje projekcję katalogu, bez materializowania historii wizyt i tekstów opinii. Liczba wszystkich wyników i strona to dwa zapytania SELECT. Między nimi dane mogą się zmienić; rezerwacja zawsze ponownie sprawdza dostępność i obsługuje 409.

Warstwa aplikacyjna dostarcza ograniczony kalendarz prawidłowych lokalnych slotów na maksymalnie trzy miesiące. Zapytanie rekurencyjne SQLite generuje kandydatów wewnątrz rzeczywistych grafików i wyklucza zajęte terminy. Krótka lista wykluczeń przekazana przez `json_each` zachowuje reguły zmian czasu bez przeglądania całego kalendarza dla każdego lekarza. Dzięki temu filtr daty i sortowanie dostępności działają przed podziałem na strony. Kalendarz pomija nieistniejące i niejednoznaczne godziny zmian czasu. Parametry SQL są wiązane, a porównanie regionu używa kolacji `MEDICAL_NOCASE` z `OrdinalIgnoreCase`, również dla polskich liter.

Dostępność w katalogu jest publicznym kalendarzem lekarza. Endpoint `/api/doctors/{id}/slots` dodatkowo wyklucza równoczesne wizyty zalogowanego pacjenta. Podgląd nie rezerwuje terminu.

## Kontekst EF w panelu

Usługi aplikacyjne otrzymują `IUnitOfWorkFactory`. Każda operacja tworzy własny Unit of Work i kontekst przez `IDbContextFactory`, a następnie go zwalnia. Encje nie pozostają śledzone między zdarzeniami interfejsu. Odczyty katalogu mają własny kontekst i zwracają materializowane DTO. Nie rejestrujemy `IUnitOfWork` jako usługi obwodu Blazor.

Walidator grafiku również ma osobny kontekst odczytu; ograniczenia SQLite rozstrzygają wyścig między walidacją a zapisem. Testy ponownie używają tej samej instancji usługi, zapisują zmianę z innego kontekstu i sprawdzają świeży odczyt. Drugi test wymusza dwie jednoczesne operacje i potwierdza różne identyfikatory kontekstów.

## Zakres

Paginacja obejmuje katalog lekarzy. Listy wizyt, dokumentów i opinii zachowują dotychczasowe kontrakty list; ich stronicowanie oraz większe wolumeny wymagają osobnego etapu. Szczegóły lekarza i publiczny grafik ograniczają przedziały grafiku do bieżącego trzymiesięcznego horyzontu. Ta wersja nie wprowadza cache dostępności.

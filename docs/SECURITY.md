# Kontrola dostępu i sesje

## Role i zasoby

| Operacja | Anonimowy | Pacjent | Lekarz | Administrator |
| --- | --- | --- | --- | --- |
| Katalog lekarzy, grafiki i publiczne opinie | Tak | Tak | Tak | Tak |
| Profil, historia i rezerwacje pacjenta | Nie | Własne | Nie | Nie |
| Odczyt notatki lub recepty | Nie | Własna wizyta | Wizyta przypisana do lekarza | Nie |
| Wystawienie notatki lub recepty | Nie | Nie | Odbyta wizyta przypisana do lekarza | Nie |
| Dodanie lub usunięcie opinii | Nie | Własna opinia; obowiązują reguły odbycia wizyty | Nie | Nie |
| Dodanie grafiku | Nie | Nie | Własny | Dowolnego lekarza |
| Zmiana zdjęcia lekarza | Nie | Nie | Nie | Tak |

API domyślnie wymaga uwierzytelnienia. Wyjątki są jawne na operacjach logowania, rejestracji, publicznych odczytach katalogu i `/health/live`. Odmowa uwierzytelnienia zwraca 401, a niewłaściwa rola lub obcy zasób — 403. Brak zasobu zwraca 404. Kontrola w `UserAccess` obowiązuje również przy bezpośrednich wywołaniach usług z panelu Server.

## Tożsamość

Pacjenci rejestrują się wyłącznie z rolą `Patient`. Rejestracja nie przyjmuje roli ani identyfikatora lekarza. Konta personelu znajdują się w osobnej tabeli `StaffAccounts`; konto `Doctor` musi wskazywać istniejącego lekarza, a `Admin` nie ma takiego powiązania. Spójność ról, unikalność emaila i przypisania do lekarza wymusza również SQLite. Nieaktywne konto nie może się zalogować. Publicznego endpointu tworzenia kont personelu nie ma; seed obsługuje wyłącznie fikcyjne demo.

JWT zawiera rolę oraz odrębny `patient_id` albo `doctor_id`. Identyfikatory kont personelu i pacjentów nie są wymienne. Podpis HS256, wystawca, odbiorca i czas ważności są sprawdzane przez standardowy handler ASP.NET Core. Token jest ważny godzinę. API przy rezerwacji, profilu i opinii pobiera pacjenta z uwierzytelnionej tożsamości, a przy wystawianiu dokumentu — lekarza.

W Development Swagger udostępnia przycisk **Authorize** dla tokenu Bearer. Publiczne operacje są oznaczone bez wymogu logowania.

## Aplikacja pacjenta

WASM przechowuje token w localStorage, dołącza `Authorization: Bearer` wyłącznie do skonfigurowanego originu API i chroni prywatne trasy przez `AuthorizeRouteView`. Wylogowanie, wygaśnięcie tokenu oraz odpowiedź 401 na uwierzytelnione żądanie usuwają token i stary klucz `patientId`. Odczyt claims w przeglądarce służy interfejsowi; uprawnienia i podpis weryfikuje serwer.

Zmiany kontraktu względem pierwotnej aplikacji:

- `GET /api/appointments/upcoming` i `/history` bez identyfikatora pacjenta w ścieżce.
- Rezerwacja przyjmuje `doctorId` i `date`; `patientId` nie jest polem DTO.
- Profil, szczegóły i anulowanie wizyty oraz operacje na opiniach nie wymagają parametru `patientId`.
- `POST /api/auth/staff/login` wydaje token personelu. Formularz panelu używa osobnej sesji cookie.

## Panel personelu

Logowanie odbywa się przez Razor Pages pod `/Account/Login`. Cookie jest HttpOnly, SameSite=Strict, ma godzinny czas ważności bez przedłużania i wymaga HTTPS poza Development/Testing. Formularze logowania i wylogowania sprawdzają token CSRF. GET wylogowania pokazuje formularz; dopiero POST usuwa sesję. Parametr przekierowania nie może wyprowadzić użytkownika poza panel.

Panel i hub SignalR wymagają roli personelu. Tożsamość wewnątrz obwodu Blazor pochodzi z `AuthenticationStateProvider`, a nie z bieżącego `HttpContext`. Hub zamyka połączenie po wygaśnięciu uwierzytelnienia; usługi niezależnie sprawdzają termin sesji. Lekarz korzysta z `/doctor/appointments`, `/doctor/notes` i `/doctor/prescriptions`, bez identyfikatora lekarza w adresie. Zakończenie wizyty wymaga przypisanego lekarza i upływu pełnych 30 minut jej terminu. Dokumentację można dodać po zakończeniu wizyty.

Logowanie i rejestracja API mają wspólny limit 20 żądań na minutę z jednego adresu IP. Strona logowania personelu ma analogiczny limit w swoim procesie. Przekroczenie limitu zwraca 429.

## Zakres obecnego etapu

To lokalny projekt demonstracyjny. Token usunięty z przeglądarki pozostaje kryptograficznie ważny do wygaśnięcia. Zmiana hasła i dezaktywacja konta blokują kolejne logowania, lecz nie unieważniają już wydanych tokenów lub cookie. Wymuszone unieważnianie sesji, zarządzanie kontami, reset hasła i audyt dokumentacji są osobnymi zadaniami w `ROADMAP.md`.

Token w localStorage jest dostępny dla kodu JavaScript działającego w originie aplikacji. Docelowe publiczne wdrożenie wymaga dopracowania ochrony XSS/CSP i przepływu sesji, trwałego magazynu kluczy Data Protection oraz konfiguracji zaufanego proxy dla HTTPS i limitów IP. Reguły cyklu życia wizyty i ochronę rezerwacji opisuje [BOOKING.md](BOOKING.md).

## Odczyty i pliki

Kontekst EF nie jest współdzielony między operacjami obwodu Blazor. Odczyty katalogu są stronicowane i projektują publiczne dane bez historii klinicznej. Formularze personelu pozostają nieaktywne do pierwszego interaktywnego renderowania. Przesyłane zdjęcia są dekodowane i kodowane ponownie, a nazwy plików oraz adres publiczny kontroluje serwer; szczegóły: [MEDIA.md](MEDIA.md).

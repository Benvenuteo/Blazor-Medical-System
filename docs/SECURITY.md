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
| Lista, tworzenie, edycja i reset kont personelu | Nie | Nie | Nie | Tak |
| Zmiana własnego hasła i zakończenie własnych sesji | Nie | Tak (nie dla kont demo) | Tak (nie dla kont demo) | Tak (nie dla kont demo) |
| Placówki i przypisanie lekarzy | Nie | Nie | Nie | Tak |
| Geokodowanie adresu (`/api/locations/geocode`) | Nie | Tak | Tak | Tak |
| Logowanie Google, łączenie i odłączanie | Logowanie | Własne konto | Nie | Nie |

API domyślnie wymaga uwierzytelnienia. Wyjątki są jawne na operacjach logowania, rejestracji, publicznych odczytach katalogu oraz `/health/live` i `/health/ready`. Sonda gotowości zwraca tylko `Healthy`/`Unhealthy`, bez szczegółów błędu. Odmowa uwierzytelnienia zwraca 401, a niewłaściwa rola lub obcy zasób — 403. Brak zasobu zwraca 404. Kontrola w `UserAccess` obowiązuje również przy bezpośrednich wywołaniach usług z panelu Server.

## Tożsamość

Pacjenci rejestrują się wyłącznie z rolą `Patient`. Rejestracja nie przyjmuje roli ani identyfikatora lekarza. Konta personelu znajdują się w osobnej tabeli `StaffAccounts`; konto `Doctor` musi wskazywać istniejącego lekarza, a `Admin` nie ma takiego powiązania. Spójność ról, unikalność emaila i przypisania do lekarza wymusza również SQLite. Nieaktywne konto nie może się zalogować. Tworzenie kont personelu wymaga administratora. Seed obsługuje wyłącznie fikcyjne demo.

JWT zawiera rolę oraz odrębny `patient_id` albo `doctor_id`. Identyfikatory kont personelu i pacjentów nie są wymienne. Podpis HS256, wystawca, odbiorca i czas ważności są sprawdzane przez standardowy handler ASP.NET Core. Token jest ważny maksymalnie godzinę. Tożsamość zawiera również niepusty `security_stamp`, identyfikator konta z prefiksem `patient:` lub `staff:` i czas sesji. Walidator wymaga dokładnie jednej zgodnej tożsamości, roli i znacznika; porównuje je z aktualnym kontem w bazie. API przy rezerwacji, profilu i opinii pobiera pacjenta z uwierzytelnionej tożsamości, a przy wystawianiu dokumentu — lekarza.

W Development Swagger udostępnia przycisk **Authorize** dla tokenu Bearer. Publiczne operacje są oznaczone bez wymogu logowania.

## Aplikacja pacjenta

WASM przechowuje token w localStorage, dołącza `Authorization: Bearer` wyłącznie do skonfigurowanego originu API i chroni prywatne trasy przez `AuthorizeRouteView`. Wylogowanie, wygaśnięcie tokenu oraz odpowiedź 401 na uwierzytelnione żądanie usuwają token i stary klucz `patientId`. Odczyt claims w przeglądarce służy interfejsowi; uprawnienia i podpis weryfikuje serwer.

Zmiany kontraktu względem pierwotnej aplikacji:

- `GET /api/appointments/upcoming` i `/history` bez identyfikatora pacjenta w ścieżce.
- Rezerwacja przyjmuje `doctorId` i `date`; `patientId` nie jest polem DTO.
- Profil, szczegóły i anulowanie wizyty oraz operacje na opiniach nie wymagają parametru `patientId`.
- `POST /api/auth/staff/login` wydaje token personelu. Formularz panelu używa osobnej sesji cookie.

## Panel personelu

Logowanie odbywa się przez Razor Pages pod `/Account/Login`. Cookie jest HttpOnly, SameSite=Strict, ma godzinny czas ważności bez przedłużania i wymaga HTTPS poza Development/Testing. Formularze logowania, zmiany hasła i wylogowania sprawdzają token CSRF. GET wylogowania pokazuje formularz; dopiero POST usuwa sesję. Parametr przekierowania nie może wyprowadzić użytkownika poza panel.

Panel i hub SignalR wymagają roli personelu. Tożsamość wewnątrz obwodu Blazor pochodzi z `AuthenticationStateProvider`, a nie z bieżącego `HttpContext`. Hub zamyka połączenie po wygaśnięciu uwierzytelnienia. Stan uwierzytelnienia obwodu jest ponownie sprawdzany co 30 sekund. Niezależnie od tego każda chroniona operacja usługi sprawdza termin oraz aktualny stan konta w osobnym kontekście EF, bez pamięci podręcznej obwodu. Odebranie dostępu blokuje więc następną operację również przed odświeżeniem interfejsu. Lekarz korzysta z `/doctor/appointments`, `/doctor/notes` i `/doctor/prescriptions`, bez identyfikatora lekarza w adresie. Zakończenie wizyty wymaga przypisanego lekarza i upływu pełnych 30 minut jej terminu. Dokumentację można dodać po zakończeniu wizyty.

Logowanie i rejestracja API mają wspólny limit 20 żądań na minutę z jednego adresu IP. Strona logowania personelu ma analogiczny limit w swoim procesie; liczone są tylko wysłania formularza (POST), a samo wyświetlenie strony logowania nie zużywa limitu. Przekroczenie limitu zwraca 429.

## Konta, hasła i unieważnianie sesji

Panel `/admin/accounts` ma stronicowaną listę i wybór lekarzy bez przypisanego konta. Konto `Doctor` jest na stałe powiązane z jednym lekarzem; rola i przypisanie nie są polami edycji. Wyłączone konto nadal zajmuje przypisanie — należy je reaktywować. Konto `Admin` nie ma lekarza. DTO nie zawierają hashy haseł ani znaczników sesji. `Version` jest jawnym tokenem współbieżności, oddzielnym od `SecurityStamp`.

Administrator może utworzyć konto, zmienić nazwę/email, wyłączyć lub włączyć konto i ustawić innemu użytkownikowi hasło tymczasowe. Własny email, aktywacja i reset administracyjny są zablokowane; własne hasło zmienia się pod `/Account/ChangePassword`. Zapis i reset wymagają bieżącego `Version`; spóźniona edycja zwraca 409. Triggery SQLite nie pozwalają wyłączyć, zmienić roli ani usunąć ostatniego aktywnego administratora, również przy równoczesnych zapisach z osobnych połączeń.

Hasła personelu mają 12–128 znaków, dużą literę i cyfrę. Nowe oraz resetowane konta otrzymują `MustChangePassword`: mogą się uwierzytelnić, zmienić własne hasło i wylogować, ale chronione operacje panelu/API zwracają odmowę do czasu zmiany hasła. Zmiana wymaga aktualnego hasła i potwierdzenia nowego. Po zmianie trzeba się ponownie zalogować. Reset jest ręczny — system nie wysyła haseł ani linków emailem.

Zmiana hasła pacjenta lub personelu, administracyjny reset hasła, zmiana emaila, wyłączenie/reaktywacja konta i polecenie **Wyloguj wszystkie sesje** obracają `SecurityStamp`. Dotychczasowe JWT i cookies są odrzucane; reaktywacja nie przywraca wcześniejszych tokenów. Zmiana samej nazwy nie wylogowuje. Token logowania zawiera znacznik z tego samego odczytu konta, w którym zweryfikowano hasło, więc równoczesny reset nie może wydać nowej sesji dla starego hasła.

Zwykłe **Wyloguj** usuwa token/cookie z bieżącej przeglądarki. Jego przechwycona kopia może być użyta do końca ważności, dopóki znacznik konta nie zostanie zmieniony. **Wyloguj wszystkie sesje** unieważnia sesje tego konta na wszystkich urządzeniach, także w drugim hoście (API i panel korzystają ze wspólnej bazy). Nie ma listy pojedynczych sesji ani selektywnego odwoływania jednego urządzenia.

Pacjent korzysta z `/account/security`: zmiana hasła przez profil albo `POST /api/auth/logout-all`. Personel ma formularze `/Account/ChangePassword` i `/Account/LogoutAll`; API udostępnia także `POST /api/auth/staff/change-password`. Żadna z tych operacji nie przyjmuje identyfikatora właściciela sesji od klienta.

Migracja `AccountSessions` zachowuje hashe istniejących kont i nadaje im odrębne znaczniki. Wcześniejsze tokeny/cookies nie mają poprawnego znacznika i wymagają ponownego logowania. Konta demo nie wymagają zmiany hasła przy starcie. Pierwszego administratora oraz odzyskanie jego dostępu obsługuje narzędzie utrzymaniowe opisane w [OPERATIONS.md](OPERATIONS.md). Konta demo pozostają przeznaczone do lokalnej prezentacji.

## Zakres obecnego etapu

Projekt demonstracyjny ma audyt dokumentacji, weryfikowane kopie i testy odtwarzania. Procedury oraz zakres ochrony zapisano w [OPERATIONS.md](OPERATIONS.md).

Token w localStorage jest dostępny dla kodu JavaScript działającego w originie aplikacji. Panel ma CSP, szyfrowany trwały magazyn Data Protection dla Production i jawne zaufane proxy. Dla statycznego WASM nagłówki konfiguruje host; przykład dostarcza Nginx. Model BFF/HttpOnly dla pacjenta pozostaje decyzją osobnego wdrożenia. Reguły cyklu życia wizyty i ochronę rezerwacji opisuje [BOOKING.md](BOOKING.md).

## Odczyty i pliki

Kontekst EF nie jest współdzielony między operacjami obwodu Blazor. Odczyty katalogu są stronicowane i projektują publiczne dane bez historii klinicznej. Formularze personelu pozostają nieaktywne do pierwszego interaktywnego renderowania. Przesyłane zdjęcia są dekodowane i kodowane ponownie, a nazwy plików oraz adres publiczny kontroluje serwer; szczegóły: [MEDIA.md](MEDIA.md).

## Konta demonstracyjne

Konta z seeda demo (`patient@`, `doctor@`, `admin@example.test`) mają flagę `IsDemo`. Usługi odmawiają (403) zmiany ich hasła, emaila, profilu, aktywności, resetu przez administratora, wylogowania wszystkich sesji, resetu hasła mailem i powiązania z Google — każda z tych operacji odcięłaby kolejnego odwiedzającego. Konta demo nie podlegają też blokadzie po nieudanych logowaniach; chroni je limit żądań na adres IP. Rezerwacje, dokumentacja, opinie i zarządzanie innymi kontami działają normalnie.

## Blokada konta i równy czas odpowiedzi

Pięć błędnych haseł blokuje konto pacjenta lub personelu na 15 minut; poprawne logowanie zeruje licznik. Nieznany email, konto zablokowane i złe hasło dają ten sam komunikat i tyle samo obliczeń hasha (weryfikacja na atrapie), więc czas odpowiedzi nie zdradza istnienia konta. Limit żądań panelu liczy wyłącznie wysłania formularza logowania, nie jego wyświetlenia.

## Potwierdzenie emaila i reset hasła

Linki zawierają losowy, jednorazowy token; baza przechowuje tylko jego SHA-256. Reset jest ważny 30 minut, nowe żądanie unieważnia wcześniejsze linki, a udany reset zmienia `SecurityStamp` (wylogowuje wszystkie sesje) i zdejmuje blokadę. Żądanie resetu dla nieznanego lub demonstracyjnego adresu zwraca to samo 202 bez wysyłki. Strona potwierdzenia wymienia token jawnym POST — skanery poczty otwierające linki GET niczego nie potwierdzą.

## Logowanie przez Google

Przeglądarka otrzymuje ID token z Google Identity Services i przekazuje go z jednorazowym `nonce`. API weryfikuje podpis kluczami Google (metadane OIDC z cache), wystawcę, `aud` równe własnemu Client ID, ważność i `nonce`, wymaga `email_verified`, a następnie wydaje własny JWT. Konto dopasowuje się po stałym `sub`, nigdy po samym emailu: istniejące potwierdzone konto zwraca 409 `account_exists_link_required` i łączy je właściciel w ustawieniach po podaniu aktualnego hasła. Wyjątkiem jest niepotwierdzona rejestracja tego samego adresu — zweryfikowany przez Google właściciel skrzynki ją przejmuje, a hasło ustawione przez rejestrującego przestaje działać. Konto utworzone przez Google musi uzupełnić datę urodzenia i telefon przed rezerwacją; blokadę wymusza stan w bazie (`ProfileIncomplete`), nie claim w tokenie.

## Nagłówki dla zewnętrznych zasobów

Aplikacja pacjenta zezwala w CSP na skrypt i ramkę Google Identity Services oraz Leaflet z cdnjs (z Subresource Integrity); kafelki OpenStreetMap są obrazami `https:`. `Referrer-Policy: strict-origin-when-cross-origin` w aplikacji pacjenta i panelu wynika z wymagań Google i polityki kafelków OSM; API pozostaje przy `no-referrer`. Lokalizacja pacjenta jest zaokrąglana do ok. 1 km i nie jest zapisywana; geokodowanie idzie przez API (limit 10/min na adres), więc przeglądarka nie kontaktuje się z Nominatim.

## Odnawianie sesji pacjenta

Token pacjenta jest ważny godzinę. Otwarta aplikacja wymienia go 10 minut przed wygaśnięciem przez `POST /api/auth/refresh`. API najpierw sprawdza sesję w bazie (`SecurityStamp`, profil), więc zmiana hasła, reset lub „wyloguj wszędzie” kończą odnawianie. Czas zalogowania (`session_started`) przechodzi do nowego tokenu i ogranicza sesję do 12 godzin; żaden token nie wychodzi poza ten limit. Token pozostaje w `localStorage` — przeniesienie go do ciasteczka HttpOnly (BFF) wymaga wspólnej domeny aplikacji i API i jest opisane w planie jako dalszy krok.

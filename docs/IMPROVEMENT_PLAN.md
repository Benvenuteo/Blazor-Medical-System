# Review i plan rozszerzeń (listopad 2026)

Stan wyjściowy: commit `e596cef`. Projekt jest dojrzały jak na portfolio — warstwy, testy (unit/integration/component/E2E), CI na 2 OS, atomowe rezerwacje, unieważnianie sesji, audyt. Poniższe punkty to rzeczy do poprawy oraz dwa duże rozszerzenia użytkowe: **logowanie Google** i **wybór lekarza z mapy w promieniu ~30 km**.

---

## Część A — Rzeczy do poprawienia (review)

| # | Priorytet | Problem | Gdzie | Propozycja |
|---|---|---|---|---|
| A1 | Wysoki | Brak blokady konta po nieudanych logowaniach. Jest tylko limit 20 żądań/min na IP — atak rozproszony lub z jednego NAT‑u omija ochronę konta. Dodatkowo przy nieistniejącym emailu hash nie jest liczony (`patient is null \|\| …`), więc czas odpowiedzi zdradza, czy konto istnieje. | `WebAPI/Program.cs:108`, `PatientAuthenticationService.cs:26`, `StaffAuthenticationService.cs:18` | Pola `FailedLoginCount`, `LockoutEndUtc` w `Patient`/`StaffAccount`; blokada np. 15 min po 5 próbach; ten sam komunikat dla zablokowanego i złego hasła; weryfikacja na atrapie hasha, gdy konta brak. Blokada dotyczy tylko logowania hasłem (nie Google). Testy integracyjne. |
| A0 | Wysoki | Rejestracja nie weryfikuje emaila — można „zająć” cudzy adres; po dodaniu Google ofiara dostanie konflikt bez wyjścia. | `PatientAuthenticationService.RegisterAsync` | Pole `EmailConfirmed` + link potwierdzający (wymaga A9). Konto z niepotwierdzonym emailem może zostać przejęte przez logowanie Google z tym samym, zweryfikowanym przez Google adresem (z unieważnieniem starego hasła i sesji). |
| A2 | Wysoki | Pacjent nie ma „Zapomniałem hasła”. Jedyna droga odzyskania konta to nowa rejestracja na inny email. | `AuthController`, WASM `Login.razor` | Token resetu (losowy, hash w bazie, ważny 30 min, jednorazowy) + `IEmailSender` (SMTP / w dev zapis do pliku). Reset zmienia `SecurityStamp`. Wymaga A9. |
| A3 | Średni | Sesja pacjenta = JWT 1 h bez odświeżania. Użytkownik w trakcie rezerwacji zostaje wylogowany po godzinie; token w `localStorage` jest dostępny dla XSS (roadmapa to przyznaje). | `PatientAuthenticationService.CreateSession`, `PatientAuthenticationStateProvider` | Krótki access token (15 min) + refresh token w cookie `HttpOnly; Secure; SameSite=Strict` na ścieżce `/api/auth/refresh`, rotowany, unieważniany przez `SecurityStamp`. Docelowo BFF. |
| A4 | Średni | Martwy kod: `Doctor.AverageRating` (kolumna w bazie) i `UpdateAverageRating()` nie są używane — średnia liczona jest w zapytaniach. Ryzyko, że ktoś zacznie czytać nieaktualną kolumnę. | `Domain/Models/Doctor.cs`, snapshot migracji, `UnitTests/Mappings/MappingTests.cs:35`, `IReviewRepository.GetAverageRatingAsync` | Usunąć właściwość, metodę, test oraz nieużywane `GetAverageRatingAsync`; migracja `DropColumn`. |
| A5 | Niski | Pozostałości szablonu: `BlazorServer/Shared/SurveyPrompt.razor` (link do ankiety Microsoft), `BlazorWasm/wwwroot/sample-data/weather.json`. | jw. | Usunąć. |
| A6 | Średni | Telefon nie jest walidowany przy rejestracji (reguła istnieje tylko w `UpdatePatientDtoValidator.cs:21`, bez limitu długości). `Address` jest walidowany przy edycji, ale nigdy nie zapisywany (`PatientService.cs:41-43`). Limit hasła różni się: 100 przy rejestracji, 128 przy edycji. | `RegisterPatientDtoValidator`, `UpdatePatientDtoValidator`, `PatientService` | Wspólna reguła telefonu (`^\+?[0-9 -]{9,15}$`) w obu walidatorach; zapisać albo usunąć `Address`; ujednolicić limit hasła. |
| A7 | Średni | UI pokazuje użytkownikowi `ex.Message` (np. „Nie udało się pobrać lekarzy: Response status code does not indicate success: 500…”). | `BlazorWasm/Pages/Doctors.razor` i inne strony z `catch (Exception ex)` | Przyjazny komunikat + szczegół tylko w konsoli; wspólny helper w `ApiErrors`. |
| A8 | Niski | Gwiazdki ocen zaokrąglają w dół (`i <= AverageRating` → 4,6 pokazuje 4 gwiazdki), brak wartości liczbowej. Przycisk „Dodaj opinię” jest widoczny przy każdym lekarzu, choć API pozwala tylko po odbytej wizycie. | `Doctors.razor` | Pokazywać `4,6 ★` + pół‑gwiazdki; „Dodaj opinię” przenieść do historii wizyt (`AppointmentsHistory.razor`) przy wizytach `Completed`. |
| A9 | Średni | Brak jakichkolwiek powiadomień (potwierdzenie rezerwacji, przypomnienie dzień przed, odwołanie przez placówkę). | Application | Abstrakcja `INotificationSender` + outbox w tabeli + `BackgroundService` wysyłający; plik `.ics` w mailu potwierdzającym. |
| A10 | Niski | Zduplikowany komponent `Pagination.razor` w WASM i Server. | `BlazorWasm/Shared`, `BlazorServer/Shared` | Projekt Razor Class Library `MedicalBookingSystem.UI.Shared`. |
| A11 | Niski | Wyszukiwanie po regionie to dokładne dopasowanie tekstu (`=` z collation). „Warsz” lub „Warszawa‑Mokotów” nie znajdzie nic. | `DoctorDirectoryQueries.SearchAsync` | Lista wyboru regionów z bazy albo dopasowanie prefiksowe; docelowo zastąpione przez wyszukiwanie po lokalizacji (część C). |
| A12 | Niski | Tylko `/health/live`; brak `/health/ready` sprawdzającego bazę. | `WebAPI/Program.cs` | `AddHealthChecks().AddDbContextCheck<ApplicationDbContext>()`, `/health/ready`. |

---

## Część B — Logowanie i autoryzacja kontem Google (pacjent)

### Decyzje projektowe

- **Przepływ:** Google Identity Services (GIS) w aplikacji WASM zwraca **ID token** (JWT podpisany przez Google). WASM wysyła go do `POST /api/auth/google`. API **samo weryfikuje** token i wydaje **własny** JWT — reszta systemu (role, `SecurityStamp`, unieważnianie sesji, `ISessionValidator`) działa bez zmian.
- Nie używamy `AddGoogle()` z cookie po stronie API, bo API jest bezstanowe i obsługuje klienta SPA na innym originie.
- **Identyfikator konta Google** = claim `sub` (stały), nie email (email może się zmienić).
- **Łączenie z istniejącym kontem hasłowym:** NIE łączymy automatycznie po samym emailu. Jeśli email już istnieje → odpowiedź `409` z kodem `account_exists_link_required`; pacjent loguje się hasłem i w „Bezpieczeństwo konta” klika „Połącz z Google”, **potwierdzając aktualnym hasłem** (token w localStorage + XSS nie może po cichu podpiąć cudzego Google). Wyjątek: konto z niepotwierdzonym emailem (A0). Jeśli pacjent nie pamięta hasła — potrzebny reset (A2), dlatego A2 jest przed B.
- **Brakujące dane:** Google nie daje daty urodzenia ani telefonu, a system wymaga 18+. Nowe konto Google ma stan „profil niekompletny” **zapisany w bazie** i do czasu uzupełnienia może tylko odczytać/uzupełnić profil i wylogować się. Uwaga: dzisiejszy `AccountSessionValidator` dla pacjenta zwraca tylko `Valid`/`Invalid`; `MustChangePassword` działa wyłącznie dla personelu — mechanizm trzeba rozszerzyć, a nie skopiować.
- **Personel:** w tej iteracji bez zmian (opcjonalnie później: Google Workspace z wymuszonym claimem `hd` = domena placówki).

### Kroki

1. **Google Cloud Console** — projekt, ekran zgody OAuth (zakresy `openid email profile`), OAuth Client ID typu *Web application*; *Authorized JavaScript origins*: `http://localhost:5016`, `http://localhost` (bez portu — wymagane przez GIS) + adres produkcyjny. Client ID trafia do konfiguracji (nie jest sekretem), client secret niepotrzebny.
2. **Konfiguracja** — `Authentication:Google:ClientId` w `WebAPI/appsettings*.json` i `BlazorWasm/wwwroot/appsettings.json`; walidacja przy starcie (`ValidateOnStart`) tylko gdy funkcja włączona (`Authentication:Google:Enabled`). Uzupełnić `deploy/*.example.json` i README.
3. **Model danych** (Domain + Infrastructure):
   - nowa encja `PatientExternalLogin { Id, PatientId, Provider, Subject, Email, LinkedAtUtc }`, unikalny indeks `(Provider, Subject)`, indeks `PatientId`;
   - `Patient.PasswordHash` → nullable (`string?`), zdjęcie `IsRequired()` w `ApplicationDbContext`;
   - `Patient.DateOfBirth` → `DateTime?` (inaczej konto Google dostanie `0001-01-01`, widoczne dla personelu) — przejrzeć wszystkie miejsca użycia (mapowania, panel lekarza, walidator wieku);
   - pole `ProfileIncomplete` (`bool`, domyślnie `false`) — **odwrócona** logika celowo: `false` = wartość domyślna CLR i bazy, więc istniejące wiersze są poprawne bez `defaultValue`, a EF nie pominie wartości w INSERT. (Wariant `ProfileCompleted` z `HasDefaultValue(true)` w modelu jest pułapką: EF pominąłby `false` w INSERT i nowe konta Google ominęłyby blokadę.);
   - `UnitOfWork.cs:52-58`: dodać mapowanie naruszenia unikalnego indeksu `PatientExternalLogin` na nowy `PersistenceConflictKind.DuplicateExternalLogin` (dziś skończyłoby się `throw;` → 500);
   - migracja `GoogleExternalLogins` + test migracji na kopii istniejącej bazy demo.
4. **Poprawki istniejącego logowania i zmiany hasła**:
   - `PatientAuthenticationService.LoginAsync`: jeśli `PasswordHash is null` → weryfikacja na atrapie hasha i ten sam komunikat „Nieprawidłowy email lub hasło”;
   - `PatientService.UpdatePatientAsync` (`PUT api/patients/profile`, `PatientService.cs:33-39`): przy `PasswordHash == null` `VerifyHashedPassword` rzuci wyjątek (500) — dodać gałąź „Ustaw hasło” wymagającą świeżego logowania; do tego nowy claim `auth_time` w `AccountClaims` (dziś go nie ma) i sprawdzenie, że logowanie było < 10 min temu.
5. **Weryfikacja tokenu Google** — pakiet `Google.Apis.Auth` (dopisać do `Directory.Packages.props`). Interfejs `IExternalTokenValidator` w Application, implementacja w Infrastructure: `GoogleJsonWebSignature.ValidateAsync(idToken, new() { Audience = [clientId] })` (biblioteka sprawdza podpis, `exp` i `iss`); dodatkowo wymagać `email_verified == true` i `nonce` (generowany przez WASM, przekazany do GIS i porównany po stronie API). Interfejs pozwala podmienić walidator w testach (bez sieci).
6. **Usługa** `ExternalAuthenticationService.SignInWithGoogleAsync(idToken, nonce)`:
   - znajdź `PatientExternalLogin` po `(google, sub)` → zaloguj (sesja jak `CreateSession`);
   - brak powiązania, ale email istnieje → `Conflict` z kodem `account_exists_link_required` (wyjątek A0);
   - brak → utwórz `Patient` (imię/nazwisko z `given_name`/`family_name`, z fallbackiem na `name`/część emaila i przycięciem do 50 znaków; `ProfileIncomplete = true`) + login zewnętrzny w jednej jednostce pracy;
   - wyścig dwóch pierwszych logowań: `PersistenceConflictException` (`DuplicateExternalLogin` lub `DuplicateAccount` na `Email`) → ponowny odczyt i zalogowanie istniejącego powiązania.
7. **Blokada niekompletnego profilu** (stan z bazy, nie z claimu ani ścieżki HTTP):
   - `AccountSessionValidator` (`:17-21`): dla pacjenta zwracać nowy `SessionStatus.ProfileIncomplete`, gdy `patient.ProfileIncomplete`;
   - `SessionGuard.RequireAsync` dostaje parametr `allowIncompleteProfile` (wzorem istniejącego `allowPasswordChange`); `true` przekazują tylko `PatientService.Get/Update`, nowa operacja uzupełnienia profilu i `SessionManagementService.RevokeAllAsync`;
   - `UserAccess.GetUserAsync` musi respektować ten parametr (inaczej zablokuje też zapis profilu);
   - API mapuje odmowę na 403 z kodem `profile_incomplete`; WASM po tym kodzie przekierowuje na uzupełnienie profilu;
   - jeśli dodajemy claim informacyjny dla UI, `AccountClaims.TryReadIdentity` (`:33-48`) musi go akceptować dla roli pacjenta.
8. **Endpointy** w `AuthController` (z `[EnableRateLimiting("authentication")]`; publiczne endpointy wymagają `[AllowAnonymous]`, bo `FallbackPolicy` wymaga logowania):
   - `POST /api/auth/google` `{ idToken, nonce }`, `[AllowAnonymous]` → `AuthenticationResponseDto`;
   - `POST /api/auth/google/link` (zalogowany pacjent) `{ idToken, nonce, currentPassword }` → po weryfikacji hasła dopisuje login zewnętrzny, zapis zdarzenia w audycie;
   - `DELETE /api/auth/google/link` — dozwolone tylko gdy konto ma hasło (żeby nie odciąć sobie dostępu);
   - zmiana hasła i `POST /api/auth/logout-all` pokazują listę powiązanych kont z opcją odpięcia (powiązanie przetrwa zmianę hasła — użytkownik musi o tym wiedzieć).
9. **Uzupełnienie profilu** — `PUT /api/patients/profile/complete` (data urodzenia 18+, telefon wg A6) → ustawia `ProfileIncomplete = false`, zmienia `SecurityStamp` i zwraca nowy token. (Istniejące trasy to `api/patients/profile` i `api/auth/logout-all` — nie ma `/me/` ani osobnego `logout`.)
10. **WASM** — w `index.html` skrypt `https://accounts.google.com/gsi/client`; mały moduł JS `google-signin.js` (render przycisku, callback → `DotNet.invokeMethodAsync`); komponent `GoogleSignInButton.razor` na `Login.razor` i `Register.razor`; po odpowiedzi `profile_incomplete` → przekierowanie na nową stronę `CompleteProfile.razor`; w `AccountSecurity.razor` sekcja „Połącz/Odłącz Google”. `PatientAuthenticationStateProvider.ReadPrincipal` musi tolerować nowy claim.
11. **CSP** (`deploy/nginx.conf.example`, blok aplikacji pacjenta): `script-src … https://accounts.google.com/gsi/client`, `frame-src https://accounts.google.com/gsi/`, `connect-src … https://accounts.google.com/gsi/`, `style-src … https://accounts.google.com/gsi/style`. Dla GIS ustawić też `Cross-Origin-Opener-Policy: same-origin-allow-popups`, jeśli będzie dodawany COOP. **`Referrer-Policy: no-referrer`** (`nginx.conf.example:22`) zmienić dla aplikacji pacjenta na `strict-origin-when-cross-origin` — GIS i klucze Google ograniczone do refererów go potrzebują. W dev nie ma nginx, więc dodać ten sam nagłówek CSP w trybie dev (np. `<meta http-equiv>` w `index.html` generowany z konfiguracji) i test E2E na brak naruszeń CSP — inaczej błędy wyjdą dopiero na produkcji.
12. **Testy** — unit: walidator (zły `aud`, `email_verified=false`, wygasły); integracyjne z fałszywym `IExternalTokenValidator`: nowe konto → `profile_incomplete` → blokada rezerwacji → uzupełnienie → rezerwacja OK; konflikt emaila; logowanie hasłem na konto bez hasła; odłączenie ostatniej metody logowania zabronione; wyścig dwóch pierwszych logowań. E2E: przycisk renderuje się (bez prawdziwego logowania Google).
13. **Dokumentacja** — `docs/SECURITY.md` (model zaufania do tokenu Google, brak auto‑linkowania), README (konfiguracja Client ID), `docs/API.md`.

---

## Część C — Wybór lekarza z mapy w promieniu ~30 km

### Decyzje projektowe

- Lokalizacja należy do **placówki**, nie do lekarza (lekarz może przyjmować w kilku miejscach). Nowa encja `Clinic` + relacja wiele‑do‑wielu `DoctorClinic`. Pole `Doctor.Region` zostaje na okres przejściowy, potem do usunięcia.
- **Placówka przenika cały przepływ rezerwacji**, nie tylko filtr: grafik (`DoctorSchedule.ClinicId`), sloty (`DoctorService.GetAvailableSlotsAsync`, `AvailableSlotDto`), „najbliższy termin” w katalogu i sama wizyta (`Appointment.ClinicId` zapisywane przy rezerwacji — nie odtwarzane z grafiku, bo grafik można później zmienić/usunąć). Inaczej „najbliższy termin” może wskazać placówkę 200 km dalej.
- **Współrzędne placówek:** warunki Google Maps Platform nie pozwalają trwale przechowywać wyników Geocoding API (cache maks. 30 dni) ani pokazywać ich na mapie innej niż Google. Dlatego: admin **ustawia pinezkę ręcznie** na mapie (współrzędne wskazane przez człowieka można przechowywać), a geokodowanie służy tylko jako podpowiedź. Alternatywa w pełni wolna: Nominatim/OSM + Leaflet (zgodnie z polityką użycia Nominatim — niski wolumen, User-Agent).
- Odległość w SQLite: **prostokąt ograniczający** (`lat/lng BETWEEN`, korzysta z indeksu) + dokładny **haversine** jako funkcja zarejestrowana przez `SqliteConnection.CreateFunction` — ten sam wzorzec co istniejące `CreateCollation("MEDICAL_NOCASE")` w `DoctorDirectoryQueries`. Nie zakładamy, że wbudowane funkcje matematyczne SQLite są dostępne.
- **Mapa:** Google Maps JavaScript API (zgodnie z pomysłem). Alternatywa bez kosztów i klucza: Leaflet + OpenStreetMap — interfejs JS‑interop tak zaprojektować, żeby dostawca był wymienny.
- **Prywatność:** lokalizacja użytkownika tylko za zgodą przeglądarki; do API wysyłane współrzędne zaokrąglone do 2 miejsc (~1 km); nie zapisujemy jej w bazie. NLog nie loguje query stringów (`Microsoft.*` poniżej Warn jest odrzucane), ale robi to domyślny `access_log` nginx — dlatego wyszukiwanie z lokalizacją idzie przez `POST /api/doctors/search` (alternatywnie `log_format` bez `$args`).

### Kroki

1. **Google Cloud** — włączyć *Maps JavaScript API*, *Places API (New)* (dla nowych projektów dostępna jest tylko nowa wersja) i opcjonalnie *Geocoding API* (tylko podpowiedź, patrz wyżej). Dwa klucze: **przeglądarkowy** (ograniczony do HTTP referrerów aplikacji pacjenta i do Maps JS + Places) oraz **serwerowy** (ograniczony do Geocoding, przechowywany jak `JwtSettings__SecretKey`, nigdy w repozytorium). Ustawić budżet/alerty kosztów.
2. **Model danych**: `Clinic { Id, Name, Street, City, PostalCode, Latitude (double), Longitude (double), Phone }`, `DoctorClinic { DoctorId, ClinicId }`; indeks `(Latitude, Longitude)`; `DoctorSchedule.ClinicId` i `Appointment.ClinicId`. Migracja: stare grafiki i wizyty dostają placówkę domyślną seedowaną w migracji (jasna reguła zamiast `NULL`), potem kolumny `NOT NULL`. Walidacja w `CreateScheduleDtoValidator`/usłudze: `ClinicId` grafiku musi należeć do `DoctorClinic` lekarza. Unikalny indeks `(DoctorId, Date)` i trigger nakładania grafików zostają bez zmian (lekarz nie jest w dwóch miejscach naraz). Seed demo: 3–4 placówki w różnych miastach.
3. **Backend placówek (bez UI mapy)** — `IGeocodingService` (Application) + `GoogleGeocodingService` (Infrastructure, typed `HttpClient`, timeout, retry; wynik tylko jako podpowiedź) + `FakeGeocodingService` dla testów/dev bez klucza. Serwis i API CRUD placówek, przypisanie lekarzy do placówek.
4. **Sloty i rezerwacja per placówka** — `GetAvailableSlotsAsync` i `AvailableSlotDto` zwracają `ClinicId`/nazwę; `CreateAppointmentDto` przyjmuje slot, a usługa ustala i zapisuje `Appointment.ClinicId` z grafiku w chwili rezerwacji; zmiana terminu aktualizuje placówkę.
5. **Wyszukiwanie** — `POST /api/doctors/search` z `SearchDoctorsDto` rozszerzonym o `Latitude?`, `Longitude?`, `RadiusKm?` (domyślnie 30, maks. 100); walidator: obie współrzędne albo żadna, zakresy −90..90 / −180..180. W `DoctorDirectoryQueries.SearchAsync`:
   - granice prostokąta (`Δlat = r/111.32`, `Δlng = r/(111.32·cos(lat))`) **liczone w C#** i przekazane jako parametry (SQLite bez funkcji matematycznych nie policzy `cos`);
   - `haversine` rejestrowane przez `SqliteConnection.CreateFunction(..., isDeterministic: true)` obok istniejącej `CreateCollation` (funkcje są rejestrowane ponownie przy każdym `Open`, więc działają dla `CountAsync` i `ToListAsync` dokładanych na `SqlQueryRaw`);
   - CTE `NearbyClinics`; `EligibleDoctors` filtruje lekarzy z placówką w promieniu; **`ShiftBounds` łączy grafiki z `NearbyClinics` po `s.ClinicId`**, żeby `NextAvailableSlot` dotyczył tylko placówek w promieniu;
   - nowe sortowanie `"distance"`; nowe kolumny w `DirectoryRow` jako nullable (`DistanceKm`, `ClinicName`, `ClinicLatitude/Longitude`) i w `DoctorDto`.
   - Zaktualizować benchmark (`docs/PERFORMANCE.md`, `DirectoryScaleTests`) — filtr przestrzenny nie może zepsuć zmierzonego czasu.
6. **API pinezek** — `POST /api/clinics/nearby` (lekkie DTO dla mapy); jeśli ma być dostępne przed logowaniem — `[AllowAnonymous]` jawnie (dziś `FallbackPolicy` wymaga logowania, a katalog lekarzy jest tylko dla pacjenta).
7. **Panel admina — placówki** (`BlazorServer/Pages/Admin`): strona „Placówki” (CRUD, mapa z ręcznym ustawieniem pinezki, podpowiedź z geokodowania) i przypisanie lekarzy w `Doctors.razor`. Wymaga rozszerzenia CSP panelu w `HostSecurity.cs` (dziś `script-src 'self'`) o domeny Maps z kroku 10.
8. **WASM — mapa**:
   - `wwwroot/js/doctor-map.js` (ładowanie Maps JS przez `@googlemaps/js-api-loader` lub tag `<script>` z kluczem z `appsettings.json`), funkcje `init`, `setMarkers`, `drawRadius`, `onMarkerClick → DotNet`;
   - komponent `DoctorMap.razor` (`IAsyncDisposable`, sprzątanie listenerów);
   - w `Doctors.razor` przełącznik **Lista / Mapa**; pole „Twoja lokalizacja”: przycisk „Użyj mojej lokalizacji” (`navigator.geolocation`) **lub** wpisanie adresu/miasta (Places Autocomplete); suwak promienia 5–100 km (domyślnie 30); kliknięcie pinezki → karta placówki z lekarzami, odległością i najbliższym terminem → „Zarezerwuj”.
   - Fallback: gdy brak zgody na lokalizację lub brak klucza Maps → zwykłe wyszukiwanie po mieście.
9. **Rezerwacja** — `AppointmentsCreate.razor`, potwierdzenie i historia pokazują adres placówki z `Appointment.ClinicId` + link „Nawiguj” (`https://www.google.com/maps/dir/?api=1&destination=lat,lng`).
10. **CSP / nagłówki** (`deploy/nginx.conf.example` blok pacjenta oraz `HostSecurity.cs` dla panelu), wg dokumentacji Maps JS:
    - `script-src … https://*.googleapis.com https://*.gstatic.com *.google.com`;
    - `img-src` — obecne `https:` wystarcza;
    - `connect-src … https://*.googleapis.com *.google.com https://*.gstatic.com`;
    - `style-src … https://fonts.googleapis.com`, `font-src … https://fonts.gstatic.com`;
    - `worker-src blob:`, `frame-src *.google.com`;
    - `Referrer-Policy: strict-origin-when-cross-origin` (klucz przeglądarkowy jest ograniczony do refererów — przy `no-referrer` mapa się nie załaduje);
    - jeśli aplikacja pacjenta dostanie `Permissions-Policy`, to `geolocation=(self)` (obecny `geolocation=()` z `HostSecurity` dotyczy API i panelu — dla API tak ma zostać).
11. **Testy** — unit: haversine (Warszawa–Kraków ≈ 252 km), prostokąt na wysokich szerokościach, walidator; integracyjne: lekarz w 25 km jest, w 35 km nie ma, `NextAvailableSlot` ignoruje grafik w dalekiej placówce, `Appointment.ClinicId` po rezerwacji i zmianie terminu, grafik w placówce nieprzypisanej do lekarza odrzucony, sortowanie po odległości, łączenie z filtrem specjalizacji/daty; E2E z fałszywą geolokalizacją Playwright (`context.grantPermissions(['geolocation'])` + `setGeolocation`) i mapą wyłączoną (tryb listy) — testy nie mogą zależeć od klucza Google.
12. **Dokumentacja** — `docs/ARCHITECTURE.md` (Clinic, geokodowanie, koszt zapytania), README (klucze, tryb bez klucza), aktualizacja zrzutów ekranu.

---

## Część D — Kolejne rozszerzenia użytkowe (backlog)

1. **Przypomnienia e‑mail/SMS** dzień przed wizytą + plik `.ics` „Dodaj do kalendarza” (zależy od A9).
2. **Lista oczekujących** — powiadomienie, gdy zwolni się wcześniejszy termin u wybranego lekarza.
3. **Kalendarz tygodniowy slotów** zamiast listy rozwijanej przy rezerwacji.
4. **Profile rodzinne** — rodzic rezerwuje wizyty dziecka (dziś rejestracja wymaga 18+).
5. **Typy wizyt** (konsultacja / kontrola / teleporada) o różnej długości — dziś stałe 30 min w `AppointmentSlots`.
6. **Teleporada** — link do spotkania wideo w szczegółach wizyty.
7. **Eksport dokumentacji do PDF** (notatki, recepty) dla pacjenta.
8. **PWA** (manifest + service worker) — instalacja na telefonie, powiadomienia push.
9. **Wersja językowa EN** (`IStringLocalizer`), skoro to projekt portfolio.

---

## Kolejność realizacji

| Etap | Zakres | Uzasadnienie |
|---|---|---|
| 1 | A4, A5, A6, A7, A8, A12 | Szybkie porządki, małe ryzyko, osobny PR. |
| 2 | A1 (lockout + równy czas odpowiedzi) | Bezpieczeństwo przed dodaniem nowej metody logowania. |
| 3 | A9 → A0, A2 | Infrastruktura e‑mail, potwierdzenie emaila i reset hasła — B ich potrzebuje do rozwiązania konfliktu 409. |
| 4 | B (Google) | Niezależne od mapy; wymaga migracji `Patient`. |
| 5 | C1–C6 (dane, placówka w grafiku/slotach/wizycie, zapytanie, API) | Backend mapy testowalny bez UI i bez klucza Google. |
| 6 | C7–C12 (panel placówek, UI mapy, CSP, E2E) | Wymaga klucza Maps i zmian CSP w obu hostach. |
| 7 | A3, A10, D | Większe zmiany architektury / nice‑to‑have. A11 jest zastępowane przez część C — jeśli C się opóźni, zrobić tylko listę wyboru regionów w etapie 1. |

Każdy etap = osobna gałąź i PR z zielonym CI (Linux + Windows + E2E).

---

## Weryfikacja planu

Plan sprawdził niezależny agent-recenzent na podstawie kodu (commit `e596cef`). Potwierdził A1–A12, z wyjątkiem części A4 i A6 (poprawione powyżej). Wskazał też błędy, które zostały naniesione:
- placówka w grafiku, slotach, `NextAvailableSlot` i wizycie;
- pułapka `HasDefaultValue(true)` → pole `ProfileIncomplete`;
- blokada profilu oparta na stanie w bazie (`AccountSessionValidator`/`SessionGuard`), a nie na claimie;
- poprawione trasy;
- potwierdzanie hasłem przy łączeniu kont;
- obsługa `PasswordHash == null`;
- mapowanie konfliktu w `UnitOfWork`;
- `Referrer-Policy` i pełne CSP dla Google (także panel);
- warunki Google dotyczące przechowywania wyników geokodowania;
- `access_log` nginx;
- kolejność A0/A2 przed B.

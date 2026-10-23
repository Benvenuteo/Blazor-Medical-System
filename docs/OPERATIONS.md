# Utrzymanie wersji demonstracyjnej

Projekt ma działający audyt, kopie zapasowe i konfigurację hostingu. Poniższe polecenia wykonuje operator mający dostęp do plików serwera. Narzędzie utrzymaniowe nie jest endpointem HTTP.

## Audyt dokumentacji

Tabela `ClinicalAudit` zapisuje czas UTC, konto i rolę użytkownika, operację, typ zasobu oraz identyfikatory zasobu i pacjenta. Dla `Note`, `Prescription`, `Appointment` i `AppointmentDetails` identyfikatorem zasobu jest ID wizyty; dla `PatientDocuments` — ID pacjenta. Nie zapisuje treści notatki, leków, nazwisk, haseł ani tokenów.

Audyt obejmuje utworzenie notatki/recepty, zakończenie wizyty, odczyt pojedynczego dokumentu, szczegółów wizyty i listy dokumentów. Zapis kliniczny oraz wpis audytu należą do jednej transakcji EF. Odczyt nie zwraca dokumentu, gdy zapis audytu się nie powiedzie. Odmowa dostępu nie jest oznaczana jako udany odczyt. Listy kolejek i historii zawierają metadane; treść dokumentacji otwiera się osobno.

Administrator przegląda stronicowany rejestr pod `/admin/clinical-audit` lub `GET /api/admin/clinical-audit`. Nie otrzymuje przez to dostępu do treści dokumentacji. Endpointów edycji/usuwania audytu nie ma, a SQLite blokuje zwykłe `UPDATE` i `DELETE` triggerami. Administrator pliku bazy może zmienić schemat — rejestr nie jest kryptograficznym dowodem odpornym na działania operatora. Docelowa polityka retencji i eksport do niezależnego magazynu należą do wdrożenia organizacji.

## Kopia zapasowa

Z katalogu repozytorium, po buildzie Release:

```bash
dotnet run --project MedicalBookingSystem/MedicalBookingSystem.Maintenance --configuration Release --no-build -- backup /srv/medical/data/medical.db /srv/medical/photos /srv/backups/medical-2026-10-22
dotnet run --project MedicalBookingSystem/MedicalBookingSystem.Maintenance --configuration Release --no-build -- verify /srv/backups/medical-2026-10-22
```

Dla lokalnego demo baza znajduje się w `MedicalBookingSystem/data/medical.db`, a katalog zdjęć określa `Media:StoragePath` panelu. Podaj rzeczywistą ścieżkę z konfiguracji; nie wskazuj katalogu `wwwroot`.

Kopia zawiera `medical.db`, zdjęcia wskazane przez snapshot w `photos/` i `manifest.json` z rozmiarem oraz SHA-256 każdego pliku. Snapshot powstaje przez SQLite Online Backup API i uwzględnia zatwierdzone dane z WAL. Nie kopiujemy samego pliku aktywnej bazy. SQLite może na czas wykonywania kopii blokować zapisy; przy większych bazach trzeba dobrać okno i monitorować czas operacji.

Narzędzie sprawdza sumy, schemat, `quick_check`, klucze obce i kompletność wskazanych zdjęć. Nieznane ścieżki, dowiązania w pakiecie, dodatkowe pliki i uszkodzenia są odrzucane. Manifest ma limit 1 MiB. Pakiet jest publikowany dopiero po pomyślnej weryfikacji, przez zmianę nazwy katalogu roboczego. Brak zdjęcia, np. przy równoczesnej wymianie, przerywa kopię; należy ją ponowić. Istniejący katalog docelowy nie jest nadpisywany.

SHA-256 wykrywa uszkodzenia i niezgodność plików z manifestem, ale nie uwierzytelnia jego autora. Korzystaj wyłącznie z zaufanych pakietów. Kopie zawierają dokumentację i hashe haseł: przechowuj je poza katalogiem WWW, z ograniczonym dostępem, szyfrowaniem i kopią poza serwerem zgodnie z polityką operatora. Harmonogram, rotacja oraz magazyn zewnętrzny nie są automatycznie tworzone przez aplikację.

## Odtwarzanie

```bash
dotnet run --project MedicalBookingSystem/MedicalBookingSystem.Maintenance --configuration Release --no-build -- restore /srv/backups/medical-2026-10-22 /srv/medical/recovered-2026-10-22
```

Odtwarzanie tworzy nowy katalog, bez podmiany działającej bazy. Sprawdza kopię i skopiowane pliki, zachowuje dane oraz audyt i nadaje wszystkim kontom nowe znaczniki sesji oraz wersji. Wcześniejsze JWT/cookies tracą ważność. Zapisuje też `recovery.json`; oryginalna kopia pozostaje dostępna.

Aby przełączyć hosty:

1. Zatrzymaj zapisy: wyłącz API i panel personelu.
2. Wskaż obu hostom odtworzony `medical.db`, a panelowi odtworzony `photos/`. Zachowaj dotychczasowy publiczny adres mediów; adresy zdjęć są zapisane w bazie.
3. Nałóż brakujące migracje, jeśli odtwarzasz wcześniejszą wspieraną wersję.
4. Uruchom API i panel, sprawdź `/health/live`, nowe logowanie, odczyt wizyty i zdjęcia. Poprzednią bazę zachowaj do zakończenia weryfikacji.

Klucza JWT, certyfikatu i kluczy Data Protection nie ma w pakiecie. Zarządza nimi konfiguracja hostingu. Zmiana klucza JWT również wymaga ponownego logowania. Test odtwarzania w CI zachowuje otwarte połączenie WAL, odtwarza dokumenty, audyt i zdjęcie oraz sprawdza unieważnienie sesji. Nie wyznaczamy produkcyjnego RPO/RTO na podstawie małej bazy testowej.

## Pierwszy administrator i odzyskanie dostępu

Po migracjach, przy wyłączonych hostach:

```bash
dotnet run --project MedicalBookingSystem/MedicalBookingSystem.Maintenance --configuration Release --no-build -- create-admin /srv/medical/data/medical.db admin@example.test "Administrator"
dotnet run --project MedicalBookingSystem/MedicalBookingSystem.Maintenance --configuration Release --no-build -- reset-admin /srv/medical/data/medical.db admin@example.test
```

Hasło tymczasowe wpisuje się bez wyświetlania na konsoli. Dla automatycznego uruchomienia narzędzie może czytać jedną linię stdin; nie przekazuj hasła w argumentach polecenia. Obowiązują reguły haseł personelu. Reset działa wyłącznie na istniejącym administratorze, aktywuje go i unieważnia wcześniejsze sesje. Po logowaniu wymagana jest zmiana hasła. Seed z publicznymi danymi służy wyłącznie lokalnemu demo.

## Hosting i nagłówki

`deploy/nginx.conf.example` pokazuje trzy hosty HTTPS, WebSocket dla panelu, nagłówki aplikacji pacjenta i przekazywanie adresu klienta przez pojedynczy zaufany proxy. To szablon do dostosowania nazw, certyfikatów i ścieżek. Nie uruchamia serwera ani nie publikuje projektu.

Szablony konfiguracji: `deploy/api.Production.example.json` i `deploy/staff.Production.example.json`. Skopiuj właściwy plik jako `appsettings.Production.json` do katalogu danego opublikowanego hosta. Nie zawierają sekretu JWT ani hasła certyfikatu — ustaw je osobno. W kliencie WASM skonfiguruj `MedicalBookingSystemAPIUrl` w publicznym `wwwroot/appsettings.json` przed publikacją; przykład: `deploy/patient.appsettings.example.json`. Ten plik nie może zawierać sekretów.

Ustawienia hostów ASP.NET Core:

- `ASPNETCORE_ENVIRONMENT=Production`, jawne `AllowedHosts`, wyłączone `Seed__Enabled` i `Database__InitializeOnStartup`.
- API: trwały losowy `JwtSettings__SecretKey`, właściwy origin w `Cors__AllowedOrigins__0` oraz wspólny connection string z panelem.
- Panel: `Media__PublicBaseUrl`, `Media__StoragePath`, `DataProtection__KeysPath`, `DataProtection__CertificatePath` i sekret `DataProtection__CertificatePassword`. Certyfikat PFX musi mieć klucz prywatny; służy szyfrowaniu kluczy Data Protection i może być oddzielny od certyfikatu HTTPS.
- `Proxy__KnownProxies__0`, ewentualnie kolejne indeksy: konkretne adresy IP proxy. Bez listy nagłówki forwarded są ignorowane. Obsługiwany jest jeden hop i zgodne `X-Forwarded-For`/`X-Forwarded-Proto`. Nie używaj globalnego `ASPNETCORE_FORWARDEDHEADERS_ENABLED` ani jego odpowiednika DOTNET.

Production odrzuca wildcard w `AllowedHosts`; panel dodatkowo wymaga trwałego katalogu kluczy poza web root i certyfikatu. Klucze są szyfrowane certyfikatem, a test restartu sprawdza odczyt zaszyfrowanej wartości w nowym providerze. Dostęp i kopię certyfikatu oraz jego hasła organizuje operator; utrata certyfikatu uniemożliwi odczyt kluczy.

Panel wysyła CSP blokujące skrypty inline, ramki i obiekty; style inline pozostają wymagane przez MudBlazor. API i panel ustawiają `nosniff`, ochronę ramek i referrera; odpowiedzi API i stron personelu nie są cache'owane. WASM jest aplikacją statyczną: jej polityka CSP i nagłówki należą do hosta statycznego, z przykładem w Nginx. Dla innego originu API trzeba dostosować `connect-src` i konfigurację klienta przed publikacją.

WASM nadal używa krótkotrwałego Bearer w localStorage. CSP ogranicza ryzyko XSS, ale go nie usuwa. Przejście na BFF/HttpOnly dla klienta pacjenta, zewnętrzny dostawca tożsamości, polityki organizacyjne i ocena zgodności pozostają decyzjami osobnego wdrożenia. Wersja portfolio nie deklaruje gotowości do przetwarzania rzeczywistych danych medycznych.

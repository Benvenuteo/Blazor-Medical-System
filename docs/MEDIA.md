# Zdjęcia lekarzy

Zdjęcie zastępuje wyłącznie administrator. Operacja korzysta ze zweryfikowanej tożsamości obwodu Blazor, bez odczytu bieżącego `HttpContext` i nagłówka `Host`.

## Walidacja i zapis

- Wejście: JPG/JPEG lub PNG, maksymalnie 10 MB i 4096 × 4096 pikseli, pojedyncza klatka.
- SkiaSharp odczytuje rzeczywisty format i dekoduje zawartość. Format musi zgadzać się z rozszerzeniem; niepoprawne i ucięte dane są odrzucane.
- Piksele są skalowane do maksymalnie 1024 × 1024 i kodowane ponownie jako PNG. Nazwa klienta, metadane i dane dopisane za obrazem nie są publikowane.
- Nazwa wyniku to losowy GUID. Plik powstaje poza śledzonym `wwwroot`, najpierw z rozszerzeniem `.pending`, a następnie zostaje przeniesiony pod docelową nazwę.
- Baza jest aktualizowana po zapisie obrazu. Błąd zapisu bazy usuwa nowy plik i zachowuje poprzednie zdjęcie.
- Po udanej zmianie usuwane jest poprzednie, własne i nieużywane zdjęcie. Błąd sprzątania jest logowany; nie zmienia wyniku udanej operacji.
- Proces sprzątający działa przy starcie i co godzinę. Usuwa osierocone pliki GUID oraz pliki `.pending` starsze niż 24 godziny. Pomija nowe, nadal używane i obce pliki.

Operacja zapisu pliku i SQLite nie jest jedną transakcją. Awarię procesu lub równoczesną zmianę zdjęcia obsługuje sprzątanie osieroconych plików. Nie usuwamy dowolnego pliku wskazanego przez stary URL.

## Konfiguracja panelu

| Ustawienie | Znaczenie |
| --- | --- |
| `Media__PublicBaseUrl` | Publiczny adres panelu, np. `https://staff.example.test`; wymagany poza Development |
| `Media__StoragePath` | Trwały katalog zdjęć; ścieżka względna jest liczona od katalogu aplikacji Server |

Lokalny profil ustawia `http://localhost:5187` i `data/media/doctors`. Pliki udostępniane są pod `/media/doctors/{guid}.png`, z właściwym typem treści i `X-Content-Type-Options: nosniff`. Adres musi być dostępny również z przeglądarki pacjenta. Przy własnym reverse proxy należy poprawnie przekierować tę ścieżkę i dopasować adres publiczny.

Katalog zdjęć wymaga trwałego wolumenu przy wdrożeniu kontenerowym i powinien być objęty kopią zapasową razem z bazą. Automatyczne sprzątanie nie przenosi plików ze starego katalogu `/images/doctors` ani między różnymi adresami publicznymi.

Testy obejmują poprawne obrazy, podrobione rozszerzenie, tekst zamiast obrazu, limit bajtów i wymiarów, ucięty plik, brak uprawnień, odrzucenie zapisu bazy, zastąpienie starego zdjęcia i bezpieczne sprzątanie. Scenariusz przeglądarkowy sprawdza odrzucenie fałszywego JPG i wyświetlenie zapisanego PNG.

# Pomiar odczytu katalogu

## Cel i kryteria

Odczyt katalogu ma zwracać jedną stronę, bez pobierania całej historii wizyt i tekstów opinii. Filtry i kolejność muszą być zastosowane przed paginacją; samo `Take` po pobraniu wszystkich encji nie spełnia tego warunku. Dodatkowe kryteria: stała liczba komend SELECT, prawidłowe wyniki i brak śledzonych grafów.

Test `DirectoryScaleTests` tworzy tymczasową bazę plikową SQLite: 100 lekarzy, 1400 grafików, 1000 historycznych wizyt i 1000 opinii. Pobiera trzecią stronę po 10 lekarzy, z sortowaniem po dostępności. Nowy odczyt jest rozgrzewany przed pomiarem, następnie wykonywany pięć razy. Poprzedni grafowy odczyt jest odtwarzany trzy razy; porównywane są również identyfikatory wyników. Dane są całkowicie fikcyjne.

## Wynik lokalny

Wynik z .NET 10.0.13, konfiguracja Release, Linux, SQLite. Surowe próbki: [directory-benchmark.json](directory-benchmark.json). Czasy zależą od maszyny i równoległego obciążenia; metryki nie są deklaracją przepustowości wdrożenia.

| Metryka | Nowy odczyt | Poprzedni graf |
| --- | ---: | ---: |
| Mediana próbek | 167.7 ms | 1679.1 ms |
| Rozmiar zwróconej strony | 10 | 10 po pobraniu grafu |
| Liczba połączonych wierszy przed paginacją | projekcja i LIMIT w SQLite | 140000 |

W tym lokalnym przebiegu mediana odczytu była około 10.0 razy krótsza. Pomiar wykonano osobno, bez równolegle uruchomionych testów przeglądarkowych.


Nowy odczyt wykonuje dwie komendy SELECT: liczba wszystkich wyników i ograniczona strona. Nie tworzy śledzonych encji. Zwraca 10 wierszy katalogu. Poprzednie połączenie kolekcji materializowało 100 lekarzy i 140000 połączonych wierszy przed podziałem na strony.

## Zmiana zapytania

Usunięto z odczytu katalogu wielokrotne `Include` kolekcji. Oceny agreguje SQLite, a najbliższy wolny slot jest wyznaczany przed sortowaniem i podziałem na strony. Rekurencyjne CTE generuje półgodzinne terminy wyłącznie wewnątrz rzeczywistych grafików, ograniczonych oknem rezerwacji. Zajęte przedziały sprawdza indeksowany odczyt wizyt. Lista wyjątków kalendarza zachowuje reguły strefy czasowej aplikacji.

Pierwszy prototyp przeglądał cały trzymiesięczny kalendarz dla każdego lekarza i w tym pomiarze był wolniejszy od poprzedniego odczytu. Został zastąpiony generowaniem terminów wewnątrz grafików. Testy porównują SQL z niezależnym planerem slotów także dla ułamków sekund, niepełnych przedziałów, wizyt spoza siatki, anulowanych rezerwacji, wyłączonych grafików i pracy przez północ.

## Odtworzenie i ograniczenia

Instrukcja eksportu wyniku znajduje się w [TESTING.md](TESTING.md). CI zapisuje własny JSON jako artefakt testów dla Linux i Windows. Test nie wymusza progu czasu: obciążenie współdzielonego runnera nie powinno powodować losowych błędów. Sprawdza za to wyniki, ograniczoną stronę i liczbę komend SQL.

Pomiar nie obejmuje opóźnienia sieci, renderowania interfejsu, wielu równoczesnych użytkowników ani wieloletniej dokumentacji medycznej. Większe wolumeny, cache, profile zapytań pod obciążeniem i stronicowanie pozostałych list pozostają osobnymi pracami. Implementacja katalogu używa składni SQLite; zmiana dostawcy wymaga dostosowania tego adaptera odczytu.

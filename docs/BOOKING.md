# Rezerwacje i cykl życia wizyty

## Sloty i czas placówki

Wizyta trwa **30 minut**, zaczyna się o pełnej lub połowie godziny i zajmuje przedział `[początek, koniec)`. Wizyta 10:00–10:30 pozwala zarezerwować kolejną od 10:30. Grafik 9:00–11:00 daje cztery sloty. Grafik 9:15–10:15 daje jeden: 9:30–10:00.

Cała wizyta musi mieścić się w jednym dostępnym przedziale grafiku. Nakładające się grafiki są odrzucane, a sąsiadujące przedziały są dozwolone. Grafik musi zawierać co najmniej jeden pełny slot. Anulowana wizyta zwalnia termin; pozostałe statusy blokują go w kalendarzu lekarza i pacjenta.

Rezerwacja wymaga co najmniej **24 upływających godzin** i mieści się w horyzoncie **3 miesięcy kalendarzowych**. Przy zmianie terminu warunki obowiązują również dla nowej daty, a do pierwotnej wizyty musi pozostać co najmniej 24 godziny. Dokładna granica 24 godzin jest dozwolona. Zapis tej samej daty jest operacją bez zmiany stanu.

Daty wizyt i grafików to czas placówki (`Europe/Warsaw`) bez offsetu i sufiksu `Z`. API odrzuca inne oznaczenie czasu. Sloty dotykające nieistniejącej lub niejednoznacznej godziny przy zmianie czasu są niedostępne. Wymóg 24 godzin jest liczony na osi UTC, a wynik porównywany w czasie placówki. Znaczniki utworzenia dokumentacji pozostają w UTC.

`AppointmentSlots` generuje pełne sloty, usuwa duplikaty przedziałów i pomija zajęte terminy. Dla posortowanych kandydatów i wizyt wykonuje jedno przejście po zajętych przedziałach. Ten sam algorytm zasila wyszukiwanie po dniu, sortowanie według najbliższego wolnego slotu i wybór terminu w aplikacji pacjenta.

## Dozwolone operacje

| Operacja | Stan początkowy | Warunek | Wynik |
| --- | --- | --- | --- |
| Rezerwacja | Brak wizyty | Wolny slot, brak kolizji pacjenta, poprawna data | `Scheduled` |
| Zmiana terminu | `Scheduled` | Właściciel, co najmniej 24 godziny do obecnej wizyty, dostępny nowy slot | `Scheduled` z nową datą |
| Anulowanie | `Scheduled` | Właściciel, co najmniej 24 godziny do wizyty | `Cancelled` |
| Zakończenie | `Scheduled` | Przypisany lekarz, termin wizyty skończył się | `Completed` |
| Notatka/recepta | `Completed` | Przypisany lekarz, poprawna treść, brak tego dokumentu | Dokument |
| Opinia | Co najmniej jedna odbyta wizyta | Pacjent po wizycie u tego lekarza, brak wcześniejszej opinii | Opinia |

`Completed` i `Cancelled` są stanami końcowymi. Edycja daty nie przyjmuje pola `Status`; zakończenie ma osobną operację dla lekarza. Jedna wizyta ma najwyżej jedną notatkę i jedną receptę. Pacjent może mieć jedną opinię dla danego lekarza.

## Ochrona danych przy równoczesnych żądaniach

Sprawdzenie dostępności przed zapisem służy do zwrócenia czytelnego błędu. Integralność zapisu zapewnia również SQLite:

- Filtrowane unikalne indeksy `(DoctorId, Date)` i `(PatientId, Date)` obejmują wizyty inne niż anulowane.
- Triggery odrzucają nakładanie 30-minutowych przedziałów lekarza lub pacjenta. Uwzględniają także wcześniejsze wizyty spoza obecnej siatki godzin.
- Osobne triggery odrzucają nakładające się grafiki, a unikalne indeksy chronią jedną opinię i jeden dokument danego typu.
- `Appointment.Version` jest tokenem optymistycznej współbieżności. Każdy zapis wizyty nadaje nowy GUID. Zapis ze starego odczytu nie może nadpisać zatwierdzonego stanu.

Zmiana terminu to aktualizacja jednego rekordu w transakcji `SaveChanges`. Gdy nowy termin zajmie inne żądanie, zapis zostaje odrzucony, a pierwotna wizyta pozostaje w bazie. `UnitOfWork` tłumaczy konflikty bazy na wyjątek domenowy i czyści odrzucony stan trackera. Usługa zwraca konflikt, a interfejs odświeża dostępność.

Walidacja żądań rezerwacji, zmiany terminu, grafiku, dokumentacji i opinii działa w usługach aplikacyjnych, więc obejmuje API i bezpośrednie wywołania z panelu Server. Typowe błędy zwracają 400, brak zasobu 404, a konflikt terminu/statusu lub równoczesnego zapisu — 409 z `ProblemDetails`.

## Punkty API i panel

| Punkt | Znaczenie |
| --- | --- |
| `GET /api/doctors/{id}/slots?date=YYYY-MM-DD` | Pełne, wolne sloty; data jest opcjonalna |
| `GET /api/doctors/{id}/slots?excludedAppointmentId=...` | Sloty do zmiany terminu własnej wizyty; wymagane uprawnienia właściciela |
| `GET /api/doctors?Date=...&SortBy=availability` | Lekarze z wolnym slotem danego dnia, posortowani według pierwszego wolnego terminu |
| `POST /api/appointments` | Rezerwacja wybranego slotu |
| `PUT /api/appointments/{id}` | Zmiana terminu przez pacjenta |
| `DELETE /api/appointments/{id}` | Anulowanie przez pacjenta |
| `GET /api/doctor/appointments` | Zaplanowane wizyty zalogowanego lekarza |
| `GET /api/doctor/appointments/completed` | Odbyte wizyty tego lekarza |
| `POST /api/doctor/appointments/{id}/complete` | Oznaczenie wizyty jako odbytej |

Dla zalogowanego pacjenta lista slotów wyklucza również kolizje z jego wizytami u innych lekarzy. Publiczny odczyt pokazuje kalendarz samego lekarza. Wyszukiwanie zwraca `nextAvailableSlot`, a brak daty przy sortowaniu nadal pozwala porównać najbliższe terminy.

Panel lekarza pod `/doctor/appointments` pokazuje również zaległe zaplanowane wizyty, aby można było je zakończyć. Dokumentacja jest dostępna pod `/doctor/notes` i `/doctor/prescriptions`. Świeży seed demo zawiera wizytę z poprzedniego dnia, gotową do przejścia przez ten scenariusz.

## Istniejąca baza

Migracja `AppointmentIntegrity` dodaje token wersji, indeksy i triggery. Nie anuluje ani nie przesuwa istniejących wizyt. Powtórzone nieanulowane terminy lub opinie wymagają wyjaśnienia przed nałożeniem unikalnych indeksów; migracja nie wybiera automatycznie rekordu do usunięcia. Kopię bazy i rozstrzygnięcie takich konfliktów przygotuj przed aktualizacją istniejących danych.

Długość 30 minut jest regułą modelu i ograniczeń SQL. Zmiana długości wizyty wymaga nowej migracji oraz aktualizacji planera i testów. Przy przyszłej przebudowie tabel SQLite należy zachować lub odtworzyć triggery z tej migracji. Dalsza optymalizacja zapytań i paginacja znajdują się w `ROADMAP.md`.

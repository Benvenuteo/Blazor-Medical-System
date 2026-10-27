import { test, expect } from '@playwright/test';

async function patientLogin(page) {
  await page.goto('/');
  await page.getByLabel('Email', { exact: true }).fill('patient@example.test');
  await page.getByLabel('Hasło', { exact: true }).fill('DemoPatient2026!');
  await page.getByRole('button', { name: 'Zaloguj się', exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
}
async function staffLogin(page, email = 'doctor@example.test', password = 'DemoStaff2026!', destination = 'http://localhost:5187/') {
  await page.goto('http://localhost:5187/Account/Login');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Hasło', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Zaloguj się', exact: true }).click();
  await expect(page).toHaveURL(destination);
  if (destination === 'http://localhost:5187/') await expect(page.locator('.page[aria-busy="false"]')).toBeVisible();
}

test('patient searches, books, reschedules, cancels and logs out', async ({ page }, info) => {
  const errors = []; page.on('pageerror', e => errors.push(e.message));
  await patientLogin(page);
  await page.goto('/doctors/search');
  await page.getByLabel('Sortuj według').selectOption('availability');
  await page.getByRole('button', { name: 'Szukaj', exact: true }).click();
  await expect(page.getByRole('cell', { name: 'Demonstracyjna', exact: true })).toBeVisible();
  await page.screenshot({ path: info.outputPath('patient-directory.png'), fullPage: true });
  await page.getByRole('link', { name: 'Zarezerwuj wizytę', exact: true }).click();
  const slot = page.getByLabel('Wolny termin');
  await expect(slot.locator('option')).not.toHaveCount(1);
  const original = await slot.locator('option').nth(1).getAttribute('value');
  await slot.selectOption(original);
  await page.getByRole('button', { name: 'Zarezerwuj', exact: true }).click();
  await expect(page).toHaveURL(/\/appointments$/);
  const future = page.getByRole('row').filter({ has: page.getByRole('button', { name: 'Edytuj', exact: true }) }).last();
  await future.getByRole('button', { name: 'Edytuj', exact: true }).click();
  const newSlot = page.getByLabel('Nowy termin');
  await expect(newSlot).toHaveValue(original);
  const changed = await newSlot.locator('option').nth(2).getAttribute('value');
  await newSlot.selectOption(changed);
  await page.getByRole('button', { name: 'Zapisz termin', exact: true }).click();
  await expect(page).toHaveURL(/\/appointments$/);
  await page.screenshot({ path: info.outputPath('patient-appointments.png'), fullPage: true });
  await page.getByRole('button', { name: 'Odwołaj wizytę', exact: true }).last().click();
  await page.getByRole('button', { name: 'Odwołaj', exact: true }).click();
  await expect(page.getByText('Wizytę odwołano pomyślnie.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Edytuj', exact: true })).toHaveCount(1);
  await page.getByRole('button', { name: 'Wyloguj', exact: true }).click();
  await page.goto('/appointments');
  await expect(page.getByRole('heading', { name: 'Logowanie', exact: true })).toBeVisible();
  expect(errors).toEqual([]);
});

test('doctor completes a visit and documents it; patient reads records and posts a review', async ({ page, browser }, info) => {
  const errors = []; page.on('pageerror', e => errors.push(e.message));
  await staffLogin(page);
  await page.goto('http://localhost:5187/doctor/appointments');
  await page.getByRole('button', { name: 'Oznacz jako odbytą', exact: true }).first().click();
  await expect(page.getByText('Brak zaplanowanych wizyt.', { exact: true })).toBeVisible();
  await page.goto('http://localhost:5187/doctor/notes');
  const notes = page.locator('section');
  await expect(notes).toHaveCount(2);
  await expect(notes.first().getByLabel('Notatka', { exact: true })).toBeEnabled();
  await notes.first().getByLabel('Notatka', { exact: true }).fill('Demonstracyjna notatka zapisana w prawdziwej przeglądarce.');
  await notes.nth(1).getByLabel('Notatka', { exact: true }).fill('Niezapisany szkic drugiej wizyty');
  await notes.first().getByRole('button', { name: 'Zapisz notatkę', exact: true }).click();
  await expect(notes).toHaveCount(1);
  await expect(notes.first().getByLabel('Notatka', { exact: true })).toHaveValue('Niezapisany szkic drugiej wizyty');
  await notes.first().getByLabel('Notatka', { exact: true }).fill('');
  await page.goto('http://localhost:5187/doctor/prescriptions');
  const prescription = page.locator('section').first();
  await expect(prescription.getByLabel('Lek', { exact: true })).toBeEnabled();
  await prescription.getByLabel('Lek', { exact: true }).fill('Preparat demonstracyjny');
  await prescription.getByLabel('Dawkowanie', { exact: true }).fill('Dawkowanie demonstracyjne');
  await prescription.getByLabel('Instrukcje', { exact: true }).fill('Dane wyłącznie do prezentacji.');
  await page.locator('section').nth(1).getByLabel('Lek', { exact: true }).fill('Drugi niezapisany lek');
  await prescription.getByRole('button', { name: 'Zapisz receptę', exact: true }).click();
  await expect(page.locator('section')).toHaveCount(1);
  await expect(page.locator('section').first().getByLabel('Lek', { exact: true })).toHaveValue('Drugi niezapisany lek');
  await page.locator('section').first().getByLabel('Lek', { exact: true }).fill('');
  await page.screenshot({ path: info.outputPath('doctor-documents.png'), fullPage: true });
  const patientContext = await browser.newContext({ locale: 'pl-PL', baseURL: 'http://localhost:5016' });
  const patient = await patientContext.newPage();
  await patientLogin(patient);
  await patient.goto('/historyAppointments');
  await expect(patient.getByRole('button', { name: 'Szczegóły', exact: true })).toHaveCount(2);
  await patient.getByRole('button', { name: 'Szczegóły', exact: true }).first().click();
  await expect(patient.getByText('Demonstracyjna notatka zapisana w prawdziwej przeglądarce.', { exact: false })).toBeVisible();
  await expect(patient.getByText('Preparat demonstracyjny', { exact: false })).toBeVisible();
  await patient.screenshot({ path: info.outputPath('patient-records.png'), fullPage: true });
  await patient.goto('/doctors/search');
  await patient.getByRole('button', { name: 'Szukaj', exact: true }).click();
  await patient.getByRole('button', { name: 'Dodaj opinię', exact: true }).click();
  await patient.getByLabel('Ocena (1-5)').fill('5');
  await patient.getByLabel('Komentarz', { exact: true }).fill('Opinia demonstracyjna z testu przeglądarkowego.');
  await patient.getByRole('button', { name: 'Wyślij opinię', exact: true }).click();
  await expect(patient.getByRole('listitem').filter({ hasText: 'Opinia demonstracyjna z testu przeglądarkowego.' })).toBeVisible();
  await expect(patient.getByRole('row').filter({ has: patient.getByRole('link', { name: 'Zarezerwuj wizytę', exact: true }) }).getByRole('cell').nth(5)).toHaveText('1');
  await patientContext.close();
  expect(errors).toEqual([]);
});

test('admin rejects fake photos and publishes a decoded image', async ({ page }, info) => {
  const errors = []; page.on('pageerror', e => errors.push(e.message));
  await staffLogin(page, 'admin@example.test');
  await page.goto('http://localhost:5187/admin/doctors');
  const input = page.getByLabel('Zmień zdjęcie: Anna Demonstracyjna', { exact: true });
  await expect(input).toBeEnabled();
  await input.setInputFiles({ name: 'fake.jpg', mimeType: 'image/jpeg', buffer: Buffer.from('not an image') });
  await expect(page.getByRole('alert')).toContainText('poprawny');
  const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAIAAAD8GO2jAAAALUlEQVR4nGM0qV7MQEvARFPTRy0YtWDUglELRi0YtWDUglELRi0YtWDUAioCAJBMAZL15RUTAAAAAElFTkSuQmCC', 'base64');
  await input.setInputFiles({ name: 'photo.png', mimeType: 'image/png', buffer: png });
  await expect(page.getByRole('status')).toContainText('Zdjęcie zaktualizowano');
  const photo = page.getByRole('img', { name: 'Zdjęcie: Anna Demonstracyjna', exact: true });
  await expect(photo).toHaveAttribute('src', /http:\/\/localhost:5187\/media\/doctors\/[a-f0-9]{32}\.png$/);
  await expect.poll(() => photo.evaluate(img => img.complete && img.naturalWidth > 0)).toBeTruthy();
  await page.screenshot({ path: info.outputPath('admin-directory.png'), fullPage: true });
  await page.getByRole('link', { name: 'Dodaj grafik', exact: true }).click();
  const startDate = page.getByLabel('Data rozpoczęcia', { exact: true });
  await expect(startDate).toBeEnabled();
  const day = new Date(Date.now() + 21 * 86_400_000).toLocaleDateString('sv-SE', { timeZone: 'Europe/Warsaw' });
  await startDate.fill(day);
  await page.getByLabel('Data zakończenia', { exact: true }).fill(day);
  await page.getByLabel('Godzina rozpoczęcia', { exact: true }).fill('09:00');
  await page.getByLabel('Godzina zakończenia', { exact: true }).fill('12:00');
  await page.getByRole('button', { name: 'Zapisz grafik', exact: true }).click();
  await expect(page.getByText('Grafik został zapisany.', { exact: true })).toBeVisible();
  expect(errors).toEqual([]);
});

test('registration validates age and creates a patient account', async ({ page }) => {
  await page.goto('/register');
  await page.getByLabel('Email', { exact: true }).fill('browser-registration@example.test');
  await page.getByLabel('Hasło', { exact: true }).fill('BrowserPatient2026!');
  await page.getByLabel('Imię', { exact: true }).fill('Alicja');
  await page.getByLabel('Nazwisko', { exact: true }).fill('Demonstracyjna');
  await page.getByLabel('Numer telefonu', { exact: true }).fill('+48500000001');
  const today = new Date().toLocaleDateString('sv-SE', { timeZone: 'Europe/Warsaw' });
  await page.getByLabel('Data urodzenia', { exact: true }).fill(today);
  await page.getByRole('button', { name: 'Zarejestruj się', exact: true }).click();
  await expect(page.locator('.alert-danger')).toContainText('18 lat');
  await page.getByLabel('Data urodzenia', { exact: true }).fill('1990-01-01');
  await page.getByRole('button', { name: 'Zarejestruj się', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Logowanie', exact: true })).toBeVisible();
  await page.getByLabel('Email', { exact: true }).fill('browser-registration@example.test');
  await page.getByLabel('Hasło', { exact: true }).fill('BrowserPatient2026!');
  await page.getByRole('button', { name: 'Zaloguj się', exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
});

async function changeStaffPassword(page, current, next) {
  await expect(page.getByRole('heading', { name: 'Zmiana hasła', exact: true })).toBeVisible();
  await page.getByLabel('Aktualne hasło', { exact: true }).fill(current);
  await page.getByLabel('Nowe hasło', { exact: true }).fill(next);
  await page.getByLabel('Powtórz nowe hasło', { exact: true }).fill(next);
  await page.getByRole('button', { name: 'Zmień hasło', exact: true }).click();
  await expect(page).toHaveURL(/\/Account\/Login\?passwordChanged=true$/);
}

test('admin creates and resets staff credentials; disabling account blocks an already open circuit', async ({ page, browser }) => {
  await staffLogin(page, 'admin@example.test');
  await page.goto('http://localhost:5187/admin/accounts');
  await expect(page.getByLabel('Nazwa nowego konta')).toBeEnabled();
  await page.getByLabel('Nazwa nowego konta').fill('Administrator testowy');
  await page.getByLabel('Email nowego konta').fill('browser-staff@example.test');
  await page.getByLabel('Rola', { exact: true }).selectOption('Admin');
  await page.getByLabel('Hasło tymczasowe', { exact: true }).fill('TemporaryBrowser2026!');
  await page.getByRole('button', { name: 'Utwórz konto', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Konto utworzone');
  const staffContext = await browser.newContext(); const staff = await staffContext.newPage();
  await staffLogin(staff, 'browser-staff@example.test', 'TemporaryBrowser2026!', 'http://localhost:5187/Account/ChangePassword');
  await staff.goto('http://localhost:5187/admin/accounts');
  await expect(staff).toHaveURL('http://localhost:5187/Account/ChangePassword');
  await changeStaffPassword(staff, 'TemporaryBrowser2026!', 'PermanentBrowser2026!');
  await staffLogin(staff, 'browser-staff@example.test', 'PermanentBrowser2026!');

  await page.getByRole('button', { name: 'Szukaj kont', exact: true }).click();
  const account = page.getByRole('row').filter({ hasText: 'browser-staff@example.test' });
  await account.getByRole('button', { name: 'Edytuj', exact: true }).click();
  await page.getByLabel('Nowe hasło tymczasowe', { exact: true }).fill('ResetBrowserStaff2026!');
  await page.getByRole('button', { name: 'Resetuj hasło', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Hasło zresetowane');
  await staff.goto('http://localhost:5187/admin/accounts');
  await expect(staff).toHaveURL(/\/Account\/Login/);
  await staffLogin(staff, 'browser-staff@example.test', 'ResetBrowserStaff2026!', 'http://localhost:5187/Account/ChangePassword');
  await changeStaffPassword(staff, 'ResetBrowserStaff2026!', 'PermanentReset2026!');
  await staffLogin(staff, 'browser-staff@example.test', 'PermanentReset2026!');
  await staff.goto('http://localhost:5187/admin/accounts');
  await staff.getByRole('row').filter({ hasText: 'browser-staff@example.test' }).getByRole('button', { name: 'Edytuj', exact: true }).click();
  await staff.getByLabel('Nazwa konta', { exact: true }).fill('Revoked change must not save');

  await page.getByRole('button', { name: 'Szukaj kont', exact: true }).click();
  await account.getByRole('button', { name: 'Edytuj', exact: true }).click();
  await page.getByLabel('Konto aktywne', { exact: true }).uncheck();
  await page.getByRole('button', { name: 'Zapisz konto', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Konto zapisane');
  await staff.getByRole('button', { name: 'Zapisz konto', exact: true }).click();
  await expect(staff.getByRole('alert')).toContainText('Sesja wygasła');
  await page.getByRole('button', { name: 'Szukaj kont', exact: true }).click();
  await expect(account).toContainText('Wyłączone');
  await expect(account).not.toContainText('Revoked change');
  await staffContext.close();
});

test('patient changes password and ends sessions in another browser', async ({ page, browser }) => {
  await page.goto('/register');
  await page.getByLabel('Email', { exact: true }).fill('browser-security@example.test');
  await page.getByLabel('Hasło', { exact: true }).fill('BrowserPatient2026!');
  await page.getByLabel('Imię', { exact: true }).fill('Jan');
  await page.getByLabel('Nazwisko', { exact: true }).fill('Testowy');
  await page.getByLabel('Numer telefonu', { exact: true }).fill('+48500000002');
  await page.getByLabel('Data urodzenia', { exact: true }).fill('1990-01-01');
  await page.getByRole('button', { name: 'Zarejestruj się', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Logowanie', exact: true })).toBeVisible();
  async function loginSecurity(target, password) {
    await target.goto('http://localhost:5016/');
    await target.getByLabel('Email', { exact: true }).fill('browser-security@example.test');
    await target.getByLabel('Hasło', { exact: true }).fill(password);
    await target.getByRole('button', { name: 'Zaloguj się', exact: true }).click();
    await expect(target).toHaveURL(/\/dashboard$/);
  }
  await loginSecurity(page, 'BrowserPatient2026!');
  const otherContext = await browser.newContext(); const other = await otherContext.newPage();
  await loginSecurity(other, 'BrowserPatient2026!');
  await page.goto('/account/security');
  await page.getByLabel('Aktualne hasło', { exact: true }).fill('BrowserPatient2026!');
  await page.getByLabel('Nowe hasło', { exact: true }).fill('ChangedBrowserPatient2026!');
  await page.getByLabel('Powtórz nowe hasło', { exact: true }).fill('ChangedBrowserPatient2026!');
  await page.getByRole('button', { name: 'Zmień hasło', exact: true }).click();
  await expect(page).toHaveURL(/\?sessionEnded=true$/);
  await expect(page.getByText('Sesje zakończone. Zaloguj się ponownie.', { exact: true })).toBeVisible();
  await other.goto('http://localhost:5016/appointments');
  await expect(other.getByRole('heading', { name: 'Logowanie', exact: true })).toBeVisible();
  await loginSecurity(page, 'ChangedBrowserPatient2026!');
  await loginSecurity(other, 'ChangedBrowserPatient2026!');
  await page.goto('/account/security');
  await page.getByLabel('Potwierdzam wylogowanie wszystkich sesji').check();
  await page.getByRole('button', { name: 'Wyloguj wszystkie sesje', exact: true }).click();
  await expect(page).toHaveURL(/\?sessionEnded=true$/);
  await expect(page.getByText('Sesje zakończone. Zaloguj się ponownie.', { exact: true })).toBeVisible();
  await other.goto('http://localhost:5016/appointments');
  await expect(other.getByRole('heading', { name: 'Logowanie', exact: true })).toBeVisible();
  await otherContext.close();
});

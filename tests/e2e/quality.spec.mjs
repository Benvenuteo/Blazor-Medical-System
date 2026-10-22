import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

async function checkAccessibility(page) {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(results.violations.map(v => ({ rule: v.id, nodes: v.nodes.map(n => n.target), help: v.help }))).toEqual([]);
}
async function patientLogin(page) {
  await page.goto('/');
  await page.getByLabel('Email', { exact: true }).fill('patient@example.test');
  await page.getByLabel('Hasło', { exact: true }).fill('DemoPatient2026!');
  await page.getByRole('button', { name: 'Zaloguj się', exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
}

test('patient navigation, paged documents and accessibility work on the selected viewport', async ({ page }, info) => {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto('/'); await checkAccessibility(page);
  await patientLogin(page);
  await expect(page.getByRole('heading', { name: 'Panel pacjenta', exact: true })).toBeVisible();
  await checkAccessibility(page);
  if (info.project.name === 'mobile-chromium') {
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
    await page.getByRole('button', { name: 'Otwórz menu nawigacji', exact: true }).click();
  }
  await page.getByRole('link', { name: 'Moje dokumenty', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Moje dokumenty', exact: true })).toBeVisible();
  await checkAccessibility(page);
  await page.goto('/doctors/search');
  await page.getByRole('button', { name: 'Szukaj', exact: true }).click();
  await expect(page.getByRole('cell', { name: 'Demonstracyjna', exact: true })).toBeVisible();
  await checkAccessibility(page);
  if (info.project.name === 'mobile-chromium')
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
  expect(errors).toEqual([]);
});

test('staff login, paged account and audit screens satisfy automated accessibility checks', async ({ page }) => {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto('http://localhost:5187/Account/Login'); await checkAccessibility(page);
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Hasło', { exact: true }).fill('DemoStaff2026!');
  await page.getByRole('button', { name: 'Zaloguj się', exact: true }).click();
  await expect(page).toHaveURL('http://localhost:5187/');
  await expect(page.locator('.page[aria-busy="false"]')).toBeVisible();
  await page.goto('http://localhost:5187/admin/accounts');
  await expect(page.getByLabel('Nazwa nowego konta')).toBeEnabled();
  await checkAccessibility(page);
  await page.goto('http://localhost:5187/admin/clinical-audit');
  await expect(page.getByRole('heading', { name: 'Audyt dokumentacji', exact: true })).toBeVisible();
  await checkAccessibility(page);
  expect(errors).toEqual([]);
});

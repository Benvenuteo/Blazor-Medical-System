import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.', testMatch: '**/*.spec.mjs', fullyParallel: false, workers: 1,
  retries: 0, timeout: 60_000, expect: { timeout: 15_000 },
  outputDir: '../../TestResults/e2e/browser',
  reporter: [['list'], ['html', { outputFolder: '../../TestResults/e2e/report', open: 'never' }]],
  use: { launchOptions: process.env.CHROMIUM_EXECUTABLE_PATH ? { executablePath: process.env.CHROMIUM_EXECUTABLE_PATH } : {}, browserName: 'chromium', baseURL: 'http://localhost:5016', locale: 'pl-PL', timezoneId: 'Europe/Warsaw',
    viewport: { width: 1440, height: 1000 }, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  webServer: [
    { command: 'node start-host.mjs api', url: 'http://localhost:5031/health/live', timeout: 60_000, reuseExistingServer: false },
    { command: 'node start-host.mjs staff', url: 'http://localhost:5187/Account/Login', timeout: 60_000, reuseExistingServer: false },
    { command: 'node start-host.mjs patient', url: 'http://localhost:5016', timeout: 60_000, reuseExistingServer: false }
  ]
});

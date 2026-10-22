import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.', testMatch: '**/*.spec.mjs', fullyParallel: false, workers: 1,
  retries: 0, timeout: 60_000, expect: { timeout: 15_000 },
  outputDir: '../../TestResults/e2e/browser',
  reporter: [['list'], ['html', { outputFolder: '../../TestResults/e2e/report', open: 'never' }]],
  use: { baseURL: 'http://localhost:5016', locale: 'pl-PL', timezoneId: 'Europe/Warsaw',
    viewport: { width: 1440, height: 1000 }, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [
    { name: 'chromium', testMatch: '**/workflows.spec.mjs', use: { browserName: 'chromium', launchOptions: process.env.CHROMIUM_EXECUTABLE_PATH ? { executablePath: process.env.CHROMIUM_EXECUTABLE_PATH } : {} } },
    { name: 'mobile-chromium', testMatch: '**/quality.spec.mjs', use: { browserName: 'chromium', viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, launchOptions: process.env.CHROMIUM_EXECUTABLE_PATH ? { executablePath: process.env.CHROMIUM_EXECUTABLE_PATH } : {} } },
    { name: 'firefox', testMatch: '**/quality.spec.mjs', use: { browserName: 'firefox', launchOptions: process.env.FIREFOX_EXECUTABLE_PATH ? { executablePath: process.env.FIREFOX_EXECUTABLE_PATH } : {} } }
  ],
  webServer: [
    { command: 'node start-host.mjs api', url: 'http://localhost:5031/health/live', timeout: 60_000, reuseExistingServer: false },
    { command: 'node start-host.mjs staff', url: 'http://localhost:5187/Account/Login', timeout: 60_000, reuseExistingServer: false },
    { command: 'node start-host.mjs patient', url: 'http://localhost:5016', timeout: 60_000, reuseExistingServer: false }
  ]
});

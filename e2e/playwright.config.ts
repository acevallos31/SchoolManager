import { defineConfig } from '@playwright/test';
import { assertAllowedStagingUrl } from './staging-safety';

const baseURL = assertAllowedStagingUrl(
  process.env.E2E_BASE_URL ?? 'http://localhost:4200',
  'E2E_BASE_URL'
);

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? [['github'], ['list']] : [['list']],
  globalSetup: './global-setup.ts',
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure'
  },
  outputDir: './test-results'
});

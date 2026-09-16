import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { defineConfig } from '@playwright/test';
import { assertAllowedStagingUrl } from './staging-safety';

const e2eDir = __dirname;
const repoRoot = resolve(e2eDir, '..');
const localEnvPath = resolve(repoRoot, '.env.e2e.local');

function loadLocalEnvironment(): void {
  if (!existsSync(localEnvPath)) {
    return;
  }

  for (const rawLine of readFileSync(localEnvPath, 'utf8').split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith('#') || !line.includes('=')) {
      continue;
    }

    const separator = line.indexOf('=');
    const key = line.slice(0, separator).trim();
    const value = line.slice(separator + 1).trim();
    if (key && process.env[key] === undefined) {
      process.env[key] = value;
    }
  }
}

loadLocalEnvironment();

const baseURL = assertAllowedStagingUrl(
  process.env.E2E_BASE_URL ?? 'http://localhost:4200',
  'E2E_BASE_URL'
);
const localStack = process.env.E2E_LOCAL_STACK === '1';

const webServer = localStack
  ? [
      {
        command:
          'dotnet run --project backend/SchoolManager.API/SchoolManager.API.csproj ' +
          '--configuration Release --no-launch-profile',
        cwd: repoRoot,
        url: 'http://127.0.0.1:5000/health',
        timeout: 120_000,
        reuseExistingServer: !process.env.CI
      },
      {
        command:
          'npm run prepare:staging && ' +
          'npm run start -- --configuration staging --host 127.0.0.1 --port 4200',
        cwd: resolve(repoRoot, 'frontend/schoolmanager-frontend'),
        url: 'http://127.0.0.1:4200/e2e-runtime.staging.json',
        timeout: 120_000,
        reuseExistingServer: !process.env.CI
      }
    ]
  : undefined;

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI
    ? [['github'], ['list'], ['html', { outputFolder: 'playwright-report', open: 'never' }]]
    : [['list']],
  globalSetup: './global-setup.ts',
  webServer,
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure'
  },
  outputDir: './test-results'
});

import { defineConfig } from '@playwright/test';

/**
 * Hosts de PRODUCCIÓN que jamás deben recibir pruebas E2E que toquen datos o
 * autenticación. Si E2E_BASE_URL apunta a cualquiera de estos y no se permite
 * explícitamente (E2E_ALLOW_PROD=1), Playwright aborta el run completo.
 */
const PROD_HOSTS = [
  'schoolmanager-xdxx.onrender.com',
  'schoolmanager.vercel.app',
  '.supabase.co',
  'supabase.co'
];

function assertNonProductionBaseUrl(baseUrl: string): string {
  const allow = process.env.E2E_ALLOW_PROD === '1';
  if (allow) return baseUrl;

  const lower = baseUrl.toLowerCase();
  const hit = PROD_HOSTS.find(host => lower.includes(host.toLowerCase()));
  if (hit) {
    throw new Error(
      `[E2E GUARDRAIL] E2E_BASE_URL apunta a un host de producción ('${hit}').\n` +
      `Las pruebas E2E no deben correr contra producción ni datos reales.\n` +
      `Usa un entorno de staging controlado (ver docs/ci/e2e-auth-setup.md).\n` +
      `Si de verdad quieres hacerlo, define E2E_ALLOW_PROD=1 (NO recomendado).`
    );
  }
  return baseUrl;
}

const baseURL = assertNonProductionBaseUrl(
  process.env.E2E_BASE_URL ?? 'http://localhost:4200'
);

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? [['github'], ['list']] : [['list']],
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure'
  },
  outputDir: './test-results'
});

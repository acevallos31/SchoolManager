import { test, expect } from '@playwright/test';

/**
 * E2E AUTENTICADO — requiere un entorno de STAGING controlado.
 *
 * NO se ejecuta por defecto. Para activarlo hay que (ver docs/ci/e2e-auth-setup.md):
 *   1. Preparar el build staging con npm run prepare:staging y variables E2E_*.
 *   2. Definir explícitamente:
 *        E2E_STAGING=1
 *        E2E_USER_EMAIL=...
 *        E2E_USER_PASSWORD=...
 *        E2E_BASE_URL=http://localhost:4200   (o un host de staging allowlisted)
 *
 * Guardrails:
 *   - playwright.config.ts usa allowlist: localhost/127.0.0.1 por defecto y
 *     E2E_ALLOWED_HOSTS para hosts de staging adicionales.
 *   - Los hosts conocidos de producción están prohibidos incluso si alguien
 *     intenta agregarlos a E2E_ALLOWED_HOSTS.
 *   - global-setup.ts lee el manifest real del build y valida también las URLs
 *     de Supabase y API embebidas en el frontend antes de autenticar.
 *   - Este spec se SKIPEA honestamente si no existen E2E_STAGING=1 y
 *     credenciales; un skip no se considera una ejecución autenticada válida.
 */

const EMAIL = process.env.E2E_USER_EMAIL;
const PASSWORD = process.env.E2E_USER_PASSWORD;
const STAGING = process.env.E2E_STAGING === '1';

test.describe('autenticación y navegación protegida (staging)', () => {
  test.beforeEach(() => {
    const missing = [
      !STAGING && 'E2E_STAGING=1',
      !EMAIL && 'E2E_USER_EMAIL',
      !PASSWORD && 'E2E_USER_PASSWORD'
    ].filter(Boolean);
    test.skip(
      missing.length > 0,
      `Entorno de staging E2E no configurado. Falta: ${missing.join(', ')}. ` +
        'Ver docs/ci/e2e-auth-setup.md. (Skip honesto, no falso-verde.)'
    );
  });

  test('login correcto llega al dashboard y la sesión persiste en rutas protegidas', async ({ page }) => {
    await page.goto('/login');

    await page.locator('#correo').fill(EMAIL!);
    await page.locator('#password').fill(PASSWORD!);
    await page.getByRole('button', { name: 'Entrar al sistema' }).click();

    await expect(page.getByRole('button', { name: 'Cerrar sesión' })).toBeVisible({ timeout: 15000 });
    await expect(page).toHaveURL(/\/dashboard$/);

    await page.goto('/alumnos');
    await expect(page).toHaveURL(/\/alumnos$/);
    await expect(page.getByRole('button', { name: 'Cerrar sesión' })).toBeVisible();
  });

  test('logout devuelve al login y limpia la sesión', async ({ page }) => {
    await page.goto('/login');
    await page.locator('#correo').fill(EMAIL!);
    await page.locator('#password').fill(PASSWORD!);
    await page.getByRole('button', { name: 'Entrar al sistema' }).click();

    await expect(page.getByRole('button', { name: 'Cerrar sesión' })).toBeVisible({ timeout: 15000 });
    await page.getByRole('button', { name: 'Cerrar sesión' }).click();

    await expect(page.locator('form.login-form')).toBeVisible();
  });
});

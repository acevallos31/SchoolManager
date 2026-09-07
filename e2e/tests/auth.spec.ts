import { test, expect } from '@playwright/test';

/**
 * E2E AUTENTICADO — requiere un entorno de STAGING controlado.
 *
 * NO se ejecuta por defecto. Para activarlo hay que (ver docs/ci/e2e-auth-setup.md):
 *   1. Tener un build de la app apuntando a un proyecto Supabase de staging
 *      (NUNCA producción) y a un backend de staging con datos de prueba.
 *   2. Definir explícitamente:
 *        E2E_STAGING=1          # contrato: apunto a un entorno de staging, no al default
 *        E2E_USER_EMAIL=...     # usuario de prueba creado en ese staging
 *        E2E_USER_PASSWORD=...
 *        E2E_BASE_URL=http://localhost:4200   (o la URL del staging)
 *
 * Guardrails:
 *   - playwright.config.ts ABORTA todo el run si E2E_BASE_URL es un host de
 *     producción (onrender.com, vercel.app, supabase.co) salvo E2E_ALLOW_PROD=1.
 *   - Este spec se SKIPEA (honestamente, nunca falso-verde) si no se dan
 *     E2E_STAGING=1 + credenciales. Un skip no es un pass.
 *   - Sin E2E_STAGING=1, el default del build podría apuntar a Supabase de
 *     producción; por eso exigimos el flag explícito.
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

    // Tras autenticar se entra al AppShell (dashboard) y aparece el cierre de sesión.
    await expect(page.getByRole('button', { name: 'Cerrar sesión' })).toBeVisible({ timeout: 15000 });
    await expect(page).toHaveURL(/\/dashboard$/);

    // Navegación a una ruta protegida con permiso y confirmación de que renderiza.
    await page.goto('/alumnos');
    await expect(page).toHaveURL(/\/alumnos$/);
    // La cabecera del shell sigue presente (sesión mantenida).
    await expect(page.getByRole('button', { name: 'Cerrar sesión' })).toBeVisible();
  });

  test('logout devuelve al login y limpia la sesión', async ({ page }) => {
    await page.goto('/login');
    await page.locator('#correo').fill(EMAIL!);
    await page.locator('#password').fill(PASSWORD!);
    await page.getByRole('button', { name: 'Entrar al sistema' }).click();

    await expect(page.getByRole('button', { name: 'Cerrar sesión' })).toBeVisible({ timeout: 15000 });
    await page.getByRole('button', { name: 'Cerrar sesión' }).click();

    // Tras cerrar sesión se vuelve al login.
    await expect(page.locator('form.login-form')).toBeVisible();
  });
});

import { APIRequestContext, expect, Page, test } from '@playwright/test';

const STAGING = process.env.E2E_STAGING === '1';
const SUPABASE_URL = process.env.E2E_SUPABASE_URL;
const SUPABASE_KEY = process.env.E2E_SUPABASE_PUBLISHABLE_KEY;
const API_URL = process.env.E2E_API_URL;

const ADMIN_A_EMAIL = process.env.E2E_USER_EMAIL;
const ADMIN_A_PASSWORD = process.env.E2E_USER_PASSWORD;
const VIEWER_EMAIL = process.env.E2E_VIEWER_EMAIL;
const VIEWER_PASSWORD = process.env.E2E_VIEWER_PASSWORD;
const ADMIN_B_EMAIL = process.env.E2E_ADMIN_B_EMAIL;
const ADMIN_B_PASSWORD = process.env.E2E_ADMIN_B_PASSWORD;

const INSTITUTION_A = process.env.E2E_INSTITUTION_A_ID;
const INSTITUTION_B = process.env.E2E_INSTITUTION_B_ID;
const STUDENT_A = process.env.E2E_STUDENT_A_ID;
const STUDENT_B = process.env.E2E_STUDENT_B_ID;
const STUDENT_A_NAME = process.env.E2E_STUDENT_A_NAME ?? 'Alumno E2E A';
const STUDENT_B_NAME = process.env.E2E_STUDENT_B_NAME ?? 'Alumno E2E B';

const REQUIRED = {
  E2E_STAGING: STAGING ? '1' : undefined,
  E2E_SUPABASE_URL: SUPABASE_URL,
  E2E_SUPABASE_PUBLISHABLE_KEY: SUPABASE_KEY,
  E2E_API_URL: API_URL,
  E2E_USER_EMAIL: ADMIN_A_EMAIL,
  E2E_USER_PASSWORD: ADMIN_A_PASSWORD,
  E2E_VIEWER_EMAIL: VIEWER_EMAIL,
  E2E_VIEWER_PASSWORD: VIEWER_PASSWORD,
  E2E_ADMIN_B_EMAIL: ADMIN_B_EMAIL,
  E2E_ADMIN_B_PASSWORD: ADMIN_B_PASSWORD,
  E2E_INSTITUTION_A_ID: INSTITUTION_A,
  E2E_INSTITUTION_B_ID: INSTITUTION_B,
  E2E_STUDENT_A_ID: STUDENT_A,
  E2E_STUDENT_B_ID: STUDENT_B
};

function missingConfiguration(): string[] {
  return Object.entries(REQUIRED)
    .filter(([, value]) => !value)
    .map(([key]) => key);
}

async function loginUi(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.locator('#correo').fill(email);
  await page.locator('#password').fill(password);
  await page.getByRole('button', { name: 'Entrar al sistema' }).click();
  await expect(page.getByRole('button', { name: 'Cerrar sesión' })).toBeVisible({ timeout: 15000 });
}

async function accessToken(
  request: APIRequestContext,
  email: string,
  password: string
): Promise<string> {
  const response = await request.post(`${SUPABASE_URL}/auth/v1/token?grant_type=password`, {
    headers: {
      apikey: SUPABASE_KEY!,
      'Content-Type': 'application/json'
    },
    data: { email, password }
  });

  expect(response.status(), 'Supabase Auth debe emitir un token E2E válido').toBe(200);
  const payload = await response.json();
  expect(typeof payload.access_token).toBe('string');
  return payload.access_token as string;
}

function bearer(token: string): Record<string, string> {
  return { Authorization: `Bearer ${token}` };
}

test.describe('044D — permisos y aislamiento institucional real', () => {
  test.beforeEach(() => {
    const missing = missingConfiguration();
    test.skip(
      missing.length > 0,
      `Fixture 044D incompleto. Falta: ${missing.join(', ')}. ` +
        'Ejecuta primero los seeds locales 044C y 044D.'
    );
  });

  test('credenciales incorrectas no crean sesión', async ({ page }) => {
    await page.goto('/login');
    await page.locator('#correo').fill(ADMIN_A_EMAIL!);
    await page.locator('#password').fill(`${ADMIN_A_PASSWORD!}-incorrecta`);
    await page.getByRole('button', { name: 'Entrar al sistema' }).click();

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('alert')).toContainText('Correo o contrasena incorrectos.');
    await expect(page.getByRole('button', { name: 'Cerrar sesión' })).toHaveCount(0);
  });

  test('una llamada a la API sin token devuelve 401', async ({ request }) => {
    const response = await request.get(`${API_URL}/api/Alumnos/${STUDENT_A}`);
    expect(response.status()).toBe(401);
  });

  test('usuario de consulta ve datos pero no acciones de escritura', async ({ page }) => {
    await loginUi(page, VIEWER_EMAIL!, VIEWER_PASSWORD!);
    await page.goto('/alumnos');

    await expect(page.getByRole('button', { name: STUDENT_A_NAME })).toBeVisible();
    await expect(page.getByRole('button', { name: '+ Nuevo Alumno' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Desactivar' })).toHaveCount(0);
  });

  test('usuario de consulta recibe 403 si intenta crear un alumno por API', async ({ request }) => {
    const token = await accessToken(request, VIEWER_EMAIL!, VIEWER_PASSWORD!);
    const response = await request.post(`${API_URL}/api/Alumnos`, {
      headers: bearer(token),
      data: {
        institucionId: INSTITUTION_A,
        nombres: 'No debe',
        apellidos: 'Crearse',
        tipoIdentificacion: 'identidad',
        numeroIdentificacion: 'E2E-044D-DENEGADO'
      }
    });

    expect(response.status()).toBe(403);
  });

  test('admin A solo obtiene su alumno y no puede leer el alumno de B', async ({ request }) => {
    const token = await accessToken(request, ADMIN_A_EMAIL!, ADMIN_A_PASSWORD!);

    const own = await request.get(`${API_URL}/api/Alumnos/${STUDENT_A}`, {
      headers: bearer(token)
    });
    expect(own.status()).toBe(200);
    const ownPayload = await own.json();
    expect(ownPayload.id).toBe(STUDENT_A);
    expect(ownPayload.institucionId).toBe(INSTITUTION_A);
    expect(ownPayload.nombreCompleto).toBe(STUDENT_A_NAME);

    const foreign = await request.get(`${API_URL}/api/Alumnos/${STUDENT_B}`, {
      headers: bearer(token)
    });
    expect(foreign.status()).toBe(404);
  });

  test('admin B solo obtiene su alumno y no puede leer el alumno de A', async ({ request }) => {
    const token = await accessToken(request, ADMIN_B_EMAIL!, ADMIN_B_PASSWORD!);

    const own = await request.get(`${API_URL}/api/Alumnos/${STUDENT_B}`, {
      headers: bearer(token)
    });
    expect(own.status()).toBe(200);
    const ownPayload = await own.json();
    expect(ownPayload.id).toBe(STUDENT_B);
    expect(ownPayload.institucionId).toBe(INSTITUTION_B);
    expect(ownPayload.nombreCompleto).toBe(STUDENT_B_NAME);

    const foreign = await request.get(`${API_URL}/api/Alumnos/${STUDENT_A}`, {
      headers: bearer(token)
    });
    expect(foreign.status()).toBe(404);
  });

  test('la vista de alumnos de A incluye su matrícula académica y no filtra datos de B', async ({ page }) => {
    await loginUi(page, ADMIN_A_EMAIL!, ADMIN_A_PASSWORD!);
    await page.goto('/alumnos');

    const rowA = page.locator('tr.fila-alumno').filter({ hasText: STUDENT_A_NAME });
    await expect(rowA).toBeVisible();
    await expect(rowA).toContainText('Ciclo E2E 2026 A');
    await expect(rowA).toContainText('1er Grado E2E A');
    await expect(rowA).toContainText('Sección E2E A');
    await expect(page.getByText(STUDENT_B_NAME, { exact: true })).toHaveCount(0);
  });
});

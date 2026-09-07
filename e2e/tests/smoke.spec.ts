import { test, expect } from '@playwright/test';

/**
 * Smoke E2E NO autenticado sobre la página pública de login (/login).
 * Es la única prueba que se ejecuta SIEMPRE: no requiere credenciales, no toca
 * Supabase Auth (la página de login no dispara red de autenticación al renderizar)
 * y por tanto es segura incluso contra un build local sin backend.
 *
 * Valida que la aplicación Angular realmente arranca (sin crash de JS), que el
 * SPA sirve la ruta /login y que el formulario de acceso es operable.
 */
test('login público: el SPA arranca y se renderiza el formulario de acceso', async ({ page }) => {
  await page.goto('/login');

  // Hero (columna izquierda) presente.
  await expect(page.locator('h1')).toContainText('Administra matriculas');

  // Formulario de acceso operable.
  const form = page.locator('form.login-form');
  await expect(form).toBeVisible();
  await expect(page.locator('#correo')).toBeVisible();
  await expect(page.locator('#password')).toBeVisible();

  // El botón de envío existe y está habilitado (no bloqueado por "cargando").
  const submit = page.getByRole('button', { name: 'Entrar al sistema' });
  await expect(submit).toBeVisible();
  await expect(submit).toBeEnabled();

  // No debe haber ningún error de aplicación al cargar (caja de error oculta).
  await expect(page.locator('form .error')).toHaveCount(0);
});

/**
 * Comportamiento real: Angular añade `novalidate` a los formularios, por lo que
 * la validación HTML5 nativa (type=email / required) NO bloquea el submit. El
 * login valida en login.ts que ambos campos estén rellenos y muestra el mensaje
 * con role="alert". Este test afirma ese comportamiento + la accesibilidad del
 * aviso de error.
 */
test('login público: campos vacíos muestran aviso accesible y no navegan', async ({ page }) => {
  await page.goto('/login');
  await page.getByRole('button', { name: 'Entrar al sistema' }).click();

  // El aviso de error se anuncia de forma asertiva (role="alert") y es visible.
  const alert = page.locator('form .error[role="alert"]');
  await expect(alert).toBeVisible();
  await expect(alert).toContainText('Ingresa tu correo y contrasena para continuar.');

  // La URL sigue en /login (no navegó a /dashboard).
  await expect(page).toHaveURL(/\/login$/);
});

/**
 * Sin sesión, una ruta protegida debe redirigir al login (guard de permisos).
 * No toca red de autenticación: solo valida el guard del router.
 */
test('login público: ruta protegida redirige a /login sin sesión', async ({ page }) => {
  await page.goto('/dashboard');
  await expect(page.locator('form.login-form')).toBeVisible({ timeout: 15000 });
});

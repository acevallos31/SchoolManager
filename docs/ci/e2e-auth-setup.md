# E2E autenticado seguro (staging) — Bloque 029

> **Acción humana requerida para el E2E autenticado.** La parte *no
> autenticada* (smoke del login) ya está lista y corre en local sin
> credenciales. El E2E *autenticado* necesita un entorno de staging controlado
> que NO se puede provisionar desde el repo ni contra producción.

## Lo que ya funciona (sin credenciales)

El harness Playwright vive en `e2e/`, aislado del frontend Angular:

```bash
cd e2e
npm install
npx playwright install chromium
E2E_BASE_URL=http://localhost:4200 npm test        # smoke público (/login)
```

El spec `smoke.spec.ts` valida que el SPA arranca, sirve `/login` y renderiza el
formulario de acceso. No usa Supabase Auth ni datos, por lo que es seguro contra
cualquier build local.

**Guardrails de seguridad integrados** (no opcionales):
- `playwright.config.ts` **aborta todo el run** si `E2E_BASE_URL` apunta a un
  host de producción (`onrender.com`, `vercel.app`, `supabase.co`), salvo que se
  fuerce con `E2E_ALLOW_PROD=1` (explícitamente no recomendado).
- `auth.spec.ts` solo se activa con `E2E_STAGING=1` + credenciales; si falta
  algo, se **skipea** (nunca falso-verde).

## Qué se necesita para activar el E2E autenticado (acciones humanas)

El E2E autenticado (`auth.spec.ts`) exige un **entorno de staging controlado**,
porque la app por defecto (entornos commiteados) apunta a Supabase de producción
`pzhcpdznjoyukbhhodjz.supabase.co` — y autenticar ahí tocaría producción, lo que
está prohibido.

1. **Proyecto Supabase de staging** (no el de producción): puede ser un proyecto
   nuevo de Supabase cloud o `supabase start` local (Docker).
   - Aplica el esquema y **siembra SOLO datos de prueba** (ciclos, alumnos,
     planes, un usuario con los permisos de la demo).
   - Anota `SUPABASE_URL` y una clave publicable de staging. En Vercel puedes\n     guardarla como `SUPABASE_PUBLISHABLE_KEY` (preferido) o\n     `SUPABASE_ANON_KEY` (nombre compatible).
2. **Backend .NET de staging** apuntando a ese Supabase (variables de entorno de
   la API: conexión/URL Supabase). Cualquiera de los dos:
   - Backend local (`dotnet run`) con `SUPABASE_URL`/`SUPABASE_ANON_KEY` del
     staging.
   - Deploy de preview en Render/Vercel apuntando al staging (sin tocar el
     servicio de producción `schoolmanager-xdxx`).
3. **Build del frontend apuntando a staging**: añade
   `src/environments/environment.staging.ts` (con URL/anon key del staging) y
   construye con `--configuration staging`, o edita `environment.ts` solo en un
   clone de staging (nunca commitees secretos: `.gitignore` ya cubre
   `**/environment.secret.ts`).
4. **Credenciales de prueba**: crea un usuario de prueba en el staging (rol con
   permisos `academico.*`, `configuracion.*`) y pásalo vía env:
   `E2E_USER_EMAIL`, `E2E_USER_PASSWORD`.

Luego ejecutas:

```bash
E2E_STAGING=1 \
E2E_BASE_URL=http://localhost:4200 \
E2E_USER_EMAIL=admin.demo@example.com \
E2E_USER_PASSWORD='...' \
npm test --prefix e2e
```

`auth.spec.ts` cubre: login correcto → dashboard → navegación a `/alumnos`,
logout → vuelta al login, y redirección de ruta protegida sin sesión.

## Por qué no se corre el E2E autenticado ahora

No existe staging provisionado, no hay credenciales de prueba y está prohibido
escribir datos de prueba en producción. El harness y los guardrails están
completos y verificados; el smoke público se ejecutó con éxito en local
(detalles en `docs/handoffs/029-calidad-sonar-e2e.md`). El E2E autenticado queda
listo para activarse en cuanto se provisione el staging con las acciones
anteriores.

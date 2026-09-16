# E2E autenticado seguro — staging aislado

El E2E autenticado de SchoolManager se ejecuta únicamente contra un entorno de
staging explícito. Producción queda fuera de alcance: no se usan usuarios reales,
no se escriben datos reales y no existe un bypass para ejecutar la suite sobre
los hosts productivos conocidos.

## Foundation 044A

La primera capa ya está preparada en código:

- Angular dispone de configuración `staging` separada de `production`.
- `npm run prepare:staging` genera `environment.staging.ts` desde variables de
  entorno y un manifest público `e2e-runtime.staging.json`.
- Los dos archivos generados están ignorados por Git y no contienen secretos
  versionados.
- El generador bloquea hosts conocidos de producción y solo acepta localhost o
  hosts añadidos explícitamente a `E2E_ALLOWED_HOSTS`.
- Playwright usa la misma política de allowlist para `E2E_BASE_URL`.
- Cuando `E2E_STAGING=1`, `global-setup.ts` consulta el manifest que realmente
  sirve el frontend y valida también `supabaseUrl` y `apiUrl` antes de ejecutar
  cualquier login.
- CI construye la configuración staging con valores locales inertes, valida el
  manifest y después construye producción verificando que el manifest E2E no se
  publique en el artefacto productivo.

Esto cierra el vacío anterior donde un frontend servido desde localhost podía
seguir llevando URLs productivas embebidas.

## Variables para preparar el frontend staging

```bash
cd frontend/schoolmanager-frontend

E2E_SUPABASE_URL=http://127.0.0.1:54321 \
E2E_SUPABASE_PUBLISHABLE_KEY='<publishable-key-del-staging>' \
E2E_API_URL=http://127.0.0.1:5000/api \
npm run prepare:staging

npm run build -- --configuration staging
```

Para un staging remoto controlado, agrega sus hostnames explícitamente:

```bash
E2E_ALLOWED_HOSTS='staging.example.com,api-staging.example.com,proyecto-staging.supabase.co'
```

Los hosts productivos conocidos de SchoolManager siguen bloqueados aunque se
intenten incluir en esa variable.

## Smoke público

El smoke no autenticado puede ejecutarse sin credenciales:

```bash
cd e2e
npm ci
npx playwright install chromium
E2E_BASE_URL=http://127.0.0.1:4200 npm test
```

Sin `E2E_STAGING=1`, `global-setup.ts` no exige el manifest porque el smoke
público no autentica ni modifica datos.

## E2E autenticado

Requiere un Supabase/Auth y backend de staging reales, además de un usuario de
prueba. Nunca usar las credenciales de producción.

```bash
cd e2e

E2E_STAGING=1 \
E2E_BASE_URL=http://127.0.0.1:4200 \
E2E_USER_EMAIL='admin.e2e@example.test' \
E2E_USER_PASSWORD='...' \
npm test
```

Antes de ejecutar los specs autenticados, Playwright exige que el frontend
publique `/e2e-runtime.staging.json` con `environment=staging`,
`production=false` y URLs de Supabase/API permitidas.

## Estrategia sin costo adicional

El camino preferido para 044 es un staging efímero en el runner o en la máquina
local: Supabase local mediante Docker/CLI, API .NET local y frontend Angular
staging local. Así no hace falta crear un segundo proyecto cloud para comenzar.

Un proyecto Supabase o servicio remoto dedicado puede incorporarse más adelante
para pruebas manuales persistentes, pero debe mantenerse completamente separado
de producción y cualquier costo debe aprobarse antes de provisionarlo.

## Lo que todavía falta después de 044A

1. Arranque seguro del backend con `ASPNETCORE_ENVIRONMENT=Staging`, incluyendo
   fail-fast si Jwt/DB apuntan a producción.
2. Supabase local/staging con migraciones 001→039 y datos exclusivamente de
   prueba.
3. Seed idempotente de instituciones, usuarios y datos mínimos de negocio.
4. Activar los casos autenticados y de aislamiento institucional.
5. Workflow manual/nightly con artifacts de Playwright.

El plan completo sigue en `docs/testing/e2e-staging-plan.md`.

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

## Foundation 044B — backend staging + Supabase local efímero

El backend también tiene ahora un modo `Staging` separado. Al arrancar con
`ASPNETCORE_ENVIRONMENT=Staging`, `StagingSafety` valida antes de registrar los
servicios que:

- `Jwt:Issuer` sea una URL permitida;
- PostgreSQL apunte únicamente a localhost o a un host de staging incluido en
  `E2E_ALLOWED_HOSTS`;
- los orígenes CORS sean de staging;
- los hosts productivos conocidos de SchoolManager no aparezcan en ninguna de
  esas dependencias.

`appsettings.Staging.json` no contiene ninguna contraseña ni connection string
utilizable por sí sola. La cadena de PostgreSQL debe llegar en runtime mediante
`ConnectionStrings__PostgreSQL`; si falta, el backend falla al arrancar en lugar
de heredar accidentalmente la base productiva.

El stack local se prepara con:

```bash
python scripts/e2e/bootstrap-local-staging.py start
```

El script requiere Docker y Supabase CLI, elimina primero los volúmenes locales
del proyecto y levanta un entorno desechable con `supabase/config.toml`. Antes
del arranque genera temporalmente `supabase/migrations/` a partir de las fuentes
canónicas del repositorio:

- `database/baseline/001_schoolmanager_fase1a.sql`;
- migraciones `007` en adelante de `database/migrations/`.

Esto reproduce el modelo usado por la instalación actual: baseline de Fase 1A
más las migraciones posteriores, sin duplicar `001–006` sobre el baseline.
`supabase/migrations/` está ignorado por Git y se elimina al detener el entorno.

Al terminar, el script imprime los valores locales necesarios para configurar
frontend y backend, incluidos `E2E_SUPABASE_URL`, la clave pública local,
`E2E_API_URL`, `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS` y, cuando Supabase CLI
la expone, `ConnectionStrings__PostgreSQL`.

Para destruir el entorno y sus datos:

```bash
python scripts/e2e/bootstrap-local-staging.py stop
```

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

Requiere además un usuario y datos deterministas de prueba. Nunca usar las
credenciales de producción.

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

## Lo que todavía falta después de 044B

1. Seed idempotente de institución, usuarios y datos mínimos de negocio.
2. Crear credenciales Auth exclusivamente locales para los perfiles de prueba.
3. Activar los casos autenticados y de aislamiento institucional.
4. Workflow manual/nightly con artifacts de Playwright.

El plan completo sigue en `docs/testing/e2e-staging-plan.md`.

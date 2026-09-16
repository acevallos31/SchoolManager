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

El backend también tiene un modo `Staging` separado. Al arrancar con
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

El runtime local se guarda en `.env.e2e.local`, también ignorado por Git. El
script no imprime claves, contraseñas, JWT secrets ni cadenas de conexión. Ese
archivo contiene únicamente valores efímeros del stack local y se elimina con
`bootstrap-local-staging.py stop`.

## 044C — seed local + Auth real

Después de levantar el stack, ejecuta:

```bash
python scripts/e2e/seed-local-staging.py
```

El seed tiene un firewall explícito: solo acepta Supabase en
`127.0.0.1/localhost:54321`. Usa la credencial privilegiada de Supabase local
en memoria y nunca la persiste. De forma idempotente crea:

- una institución local `SchoolManager E2E`;
- un usuario administrador E2E basado en la plantilla `school_admin`;
- un usuario de consulta E2E basado en `demo_viewer`;
- personas, usuarios internos y asignaciones institucionales coherentes con
  `usuarios.auth_user_id`;
- roles institucionales deterministas cuyos permisos se copian únicamente si
  son institucionales, delegables y vigentes.

Las contraseñas se generan aleatoriamente. Solo se guardan en
`.env.e2e.local`; no se muestran en consola ni se versionan. El seed tampoco
asigna `platform_admin`.

Supabase CLI local firma sus JWT con el `JWT_SECRET` efímero del stack. El
backend acepta HS256 únicamente cuando está en `Staging` y el issuer es el
Supabase loopback esperado en el puerto `54321`. Producción conserva ES256 y no
lee esa clave local.

### Ejecución autenticada local

Playwright carga automáticamente `.env.e2e.local`. Cuando detecta
`E2E_LOCAL_STACK=1`, levanta como procesos hijos la API .NET en el puerto 5000 y
Angular con configuración `staging` en el puerto 4200. Por eso no hace falta
exportar contraseñas a la consola.

```bash
cd e2e
npm ci
npx playwright install chromium
npm test -- auth.spec.ts
```

El flujo autenticado cubre login real, llegada al dashboard, navegación a una
ruta protegida y logout. El build local de Angular no ejecuta funciones Vercel,
por lo que `environment.staging.ts` desactiva exclusivamente la sincronización
de la cookie edge. Desarrollo y producción mantienen esa protección habilitada.

Al terminar:

```bash
cd ..
python scripts/e2e/bootstrap-local-staging.py stop
```

Esto elimina contenedores/volúmenes de Supabase, migraciones generadas y
`.env.e2e.local`.

## Staging remoto controlado

El generador de frontend y Playwright siguen permitiendo un staging remoto
explícitamente autorizado mediante `E2E_ALLOWED_HOSTS`:

```bash
E2E_ALLOWED_HOSTS='staging.example.com,api-staging.example.com,proyecto-staging.supabase.co'
```

Los hosts productivos conocidos de SchoolManager siguen bloqueados aunque se
intenten incluir en esa variable. El seed `seed-local-staging.py` no soporta
modo remoto por diseño.

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

## Estrategia sin costo adicional

El camino de 044 usa staging efímero en la máquina local: Supabase local mediante
Docker/CLI, API .NET local y frontend Angular staging local. No se crea un
segundo proyecto cloud ni se toca producción.

Un proyecto Supabase o servicio remoto dedicado puede incorporarse más adelante
para pruebas manuales persistentes, pero debe mantenerse completamente separado
de producción y cualquier costo debe aprobarse antes de provisionarlo.

## Siguiente expansión

Después de validar 044C con un run autenticado real, el siguiente bloque puede
sembrar datos académicos mínimos y añadir casos negativos/aislamiento
institucional. El workflow manual o nightly con artifacts de Playwright sigue
siendo una fase posterior para no convertir el E2E completo en requisito de cada
PR.

El plan completo está en `docs/testing/e2e-staging-plan.md`.

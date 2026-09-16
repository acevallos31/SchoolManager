# E2E autenticado seguro — staging aislado

El E2E autenticado de SchoolManager se ejecuta únicamente contra un entorno de
staging explícito. Producción queda fuera de alcance: no se usan usuarios reales,
no se escriben datos reales y no existe un bypass para ejecutar la suite sobre
los hosts productivos conocidos.

## 044A — frontend staging y guardrails

- Angular dispone de configuración `staging` separada de `production`.
- `npm run prepare:staging` genera `environment.staging.ts` y
  `e2e-runtime.staging.json` desde variables de entorno.
- Los artefactos generados quedan ignorados por Git.
- El generador y Playwright solo aceptan localhost/loopback por defecto o hosts
  explícitos de `E2E_ALLOWED_HOSTS`.
- Los hosts productivos conocidos permanecen bloqueados incluso si se intentan
  agregar a la allowlist.
- `global-setup.ts` valida el manifest realmente servido antes de autenticar.
- El build de producción verifica que el manifest E2E no se publique.

## 044B — backend staging + Supabase local efímero

`ASPNETCORE_ENVIRONMENT=Staging` activa `StagingSafety`, que valida antes de
arrancar:

- `Jwt:Issuer`;
- PostgreSQL;
- CORS;
- ausencia de hosts productivos conocidos.

`appsettings.Staging.json` no contiene una connection string utilizable por sí
sola. La cadena llega solo en runtime mediante `ConnectionStrings__PostgreSQL`.

El stack local se prepara con:

```bash
python scripts/e2e/bootstrap-local-staging.py start
```

El bootstrap exige Docker + Supabase CLI, reconstruye temporalmente
`supabase/migrations/` desde el baseline y las migraciones canónicas del
repositorio, levanta Supabase local y escribe `.env.e2e.local`. Tanto las
migraciones generadas como el archivo runtime están ignorados por Git y se
eliminan al detener el entorno.

## 044C — seed local + Auth real

Después de levantar el stack:

```bash
python scripts/e2e/seed-local-staging.py seed
```

El seed solo acepta Supabase en `localhost/127.0.0.1:54321`. Crea de forma
idempotente:

- institución local;
- administrador E2E;
- usuario de consulta;
- Persona/Usuario y vínculo `auth_user_id`;
- roles institucionales derivados de plantillas base.

Las contraseñas son aleatorias y permanecen exclusivamente en
`.env.e2e.local`. No se versionan, no se imprimen y no se asigna
`platform_admin`.

Playwright carga `.env.e2e.local`; cuando `E2E_LOCAL_STACK=1`, levanta la API .NET
y Angular staging como procesos hijos. El flujo 044C valida login real,
dashboard, ruta protegida y logout.

## 044D — permisos y aislamiento institucional

`e2e/support/seed-academic-local.py` extiende el fixture efímero con una segunda
institución y datos académicos deterministas para ambas instituciones:

- ciclo;
- período de matrícula;
- grado;
- jornada;
- sección;
- alumno;
- matrícula.

`academic-isolation.spec.ts` valida:

- credenciales incorrectas;
- `401` sin token;
- usuario de solo lectura sin acciones de escritura;
- `403` ante escritura sin permiso;
- aislamiento A ↔ B con `404` para recursos ajenos;
- render de matrícula académica del alumno autorizado.

El primer gate 044D detectó y permitió corregir tres problemas reales: composición
`/api/api`, falta de propagación de `institucionId` en el listado de alumnos y
refresco de error de login bajo Angular zoneless. La repetición final quedó verde.

## 044E — regresión mantenible manual/nocturna

El workflow canónico es:

`.github/workflows/e2e-regression.yml`

Admite tres formas de ejecución:

- `workflow_dispatch`: ejecución manual antes de release o después de cambios
  sensibles;
- `schedule`: ejecución nocturna diaria (`17 8 * * *`, aproximadamente 02:17 en
  Honduras / UTC-6);
- `workflow_call`: reutilización desde otros workflows.

Para validar cambios del propio harness también se dispara en PR cuando cambian
`e2e/**`, `scripts/e2e/**`, `supabase/**` o el workflow mismo. No convierte el E2E
completo en requisito de todos los PR de aplicación.

El job ejecuta el ciclo completo:

1. instala .NET, Node, npm, Supabase CLI y Chromium;
2. valida guardrails;
3. levanta Supabase local desechable;
4. siembra identidad/RBAC;
5. siembra el dataset académico multiinstitución;
6. ejecuta toda la suite Playwright;
7. destruye siempre el staging local.

Los workflows puntuales de 044C y 044D se retiraron al quedar reemplazados por
esta regresión única.

## Artifacts de fallo

En CI, Playwright genera además un reporte HTML. Si la suite falla, GitHub Actions
sube durante 14 días:

- `e2e/playwright-report`;
- `e2e/test-results`.

`test-results` contiene screenshots y traces según la configuración de
Playwright. No se sube el log crudo de Supabase para evitar persistir material
sensible accidentalmente.

## Ejecución local manual

```bash
python scripts/e2e/bootstrap-local-staging.py start
python scripts/e2e/seed-local-staging.py seed
python e2e/support/seed-academic-local.py seed
cd e2e
npm ci
./node_modules/.bin/playwright install chromium
npm test
cd ..
python scripts/e2e/bootstrap-local-staging.py stop
```

El `stop` elimina contenedores/volúmenes del stack local, migraciones generadas y
`.env.e2e.local`.

## Staging remoto controlado

El frontend y Playwright todavía permiten hosts remotos de staging mediante
`E2E_ALLOWED_HOSTS`, pero los seeds de 044C/044D son local-only por diseño. Un
proyecto cloud de staging no es requisito del E2E actual y no debe provisionarse
sin aprobación de costo y aislamiento.

## Smoke público

El smoke no autenticado sigue disponible por separado y no necesita credenciales:

```bash
cd e2e
npm ci
./node_modules/.bin/playwright install chromium
E2E_BASE_URL=http://127.0.0.1:4200 npm test -- smoke.spec.ts
```

## Estado de la deuda

Con 044A–044E, la deuda técnica interna **#13 — E2E autenticado completo en
staging seguro** queda cerrada al fusionarse 044E. El entorno es efímero,
reproducible, multiinstitución y separado de producción; la regresión puede
lanzarse manualmente, de noche o desde otro workflow y conserva artifacts de
Playwright cuando falla.

El plan histórico y su matriz están en `docs/testing/e2e-staging-plan.md`.

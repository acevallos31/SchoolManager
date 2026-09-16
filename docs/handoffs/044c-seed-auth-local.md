# Handoff 044C — seed E2E + autenticación local real

## Estado

Implementación preparada en `feature/e2e-seed-auth-local-044c`.

Este bloque todavía no debe considerarse cerrado hasta completar dos gates:

1. CI estándar + SonarCloud en verde para el HEAD final del PR.
2. Un run real local de `auth.spec.ts` contra Supabase/Auth/PostgreSQL/API/Angular de staging.

No hay cambios de producción ni migraciones nuevas.

## Qué incorpora 044C

- `scripts/e2e/seed-local-staging.py`:
  - acepta únicamente Supabase loopback `:54321`;
  - usa la credencial privilegiada local solo en memoria;
  - genera contraseñas aleatorias y las conserva únicamente en `.env.e2e.local`;
  - crea de forma idempotente una institución E2E;
  - crea/actualiza dos usuarios Auth locales;
  - vincula `auth.users.id` con `public.usuarios.auth_user_id`;
  - crea roles institucionales deterministas desde `school_admin` y `demo_viewer`;
  - copia solo permisos institucionales, delegables y vigentes;
  - no asigna `platform_admin`.
- `bootstrap-local-staging.py` deja de imprimir claves/runtime sensibles y escribe
  el runtime efímero en `.env.e2e.local`; `stop` lo elimina.
- La API acepta el HS256 usado por Supabase CLI exclusivamente cuando:
  - `ASPNETCORE_ENVIRONMENT=Staging`;
  - el issuer es loopback en `:54321`;
  - existe `JWT_SECRET` efímero de al menos 32 bytes.
  Producción conserva ES256.
- Angular staging desactiva únicamente la sincronización con `/api/auth/session`
  porque `ng serve` local no hospeda la función Vercel. Producción y desarrollo
  conservan el flujo edge actual.
- Playwright carga `.env.e2e.local` y, en modo local, orquesta API + Angular antes
  de ejecutar los specs.
- CI valida sintaxis/guardrails del seed y Sonar continúa analizando el tooling
  de forma estática sin exigirle cobertura de Coverlet/LCOV.

## Validación prevista

```bash
python scripts/e2e/bootstrap-local-staging.py start
python scripts/e2e/seed-local-staging.py
cd e2e
npm ci
npx playwright install chromium
npm test -- auth.spec.ts
cd ..
python scripts/e2e/bootstrap-local-staging.py stop
```

`auth.spec.ts` debe validar:

- credenciales correctas -> `/dashboard`;
- sesión persistente al navegar a `/alumnos`;
- logout -> `/login`;
- ausencia de sesión posterior al logout.

## Guardrails que no deben relajarse

- ningún host de producción en staging;
- ningún secreto versionado o impreso;
- ningún usuario real;
- ningún acceso a Supabase cloud desde el seed local;
- ningún `platform_admin` automático;
- ninguna migración aplicada a producción;
- no mergear mientras CI/Sonar o el run autenticado real estén pendientes.

## Siguiente checkpoint después de cerrar 044C

044D puede ampliar el seed con datos académicos mínimos y escenarios negativos,
incluido el aislamiento institucional. El workflow manual/nightly con artifacts
queda para una fase posterior para evitar que el E2E completo corra en cada PR.

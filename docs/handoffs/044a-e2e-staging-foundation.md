# Handoff 044A — Foundation de staging E2E

## Objetivo

Preparar la primera capa segura para E2E autenticado sin crear infraestructura
pagada ni tocar producción.

## Rama

`feature/e2e-staging-foundation-044`

Base: `main` después del cierre de 042/043A/043B y de la consolidación documental.

## Cambios

- Angular incorpora configuración `staging` independiente de `production`.
- `frontend/schoolmanager-frontend/scripts/generate-staging-environment.mjs`
  genera en runtime:
  - `environment.staging.ts`;
  - `e2e-runtime.staging.json`.
- Los artefactos generados quedan ignorados por Git.
- El generador exige `E2E_SUPABASE_URL`, `E2E_SUPABASE_PUBLISHABLE_KEY` y
  `E2E_API_URL`.
- Las URLs solo aceptan localhost/127.0.0.1/::1 por defecto o hosts agregados a
  `E2E_ALLOWED_HOSTS`.
- Los hosts productivos conocidos quedan bloqueados explícitamente.
- Playwright reutiliza la misma política mediante `e2e/staging-safety.ts`.
- `global-setup.ts` valida el manifest real servido por el frontend cuando
  `E2E_STAGING=1`; así una URL local no puede ocultar un build que internamente
  apunte a Supabase/API de producción.
- CI construye `staging` con valores locales inertes y verifica que el manifest
  no aparezca en el build de producción.

## Seguridad

No se añadió ningún secret, password, service-role key ni connection string.
No se creó proyecto Supabase cloud, no se ejecutaron migraciones y no se tocó
ningún dato real.

La allowlist no tiene un bypass equivalente al antiguo `E2E_ALLOW_PROD=1`.
Producción queda prohibida para esta suite.

## Validación esperada en CI

- `git diff --check`;
- backend/API/DB existentes sin regresión;
- frontend unit tests + producción;
- build Angular `staging` generado con endpoints locales inertes;
- manifest `e2e-runtime.staging.json` presente solo en staging;
- `npx playwright test --list` carga la configuración E2E;
- Sonar Quality Gate sin bajar thresholds.

## Qué NO cierra 044A

El E2E autenticado todavía no puede ejecutarse de extremo a extremo porque aún
faltan:

1. backend local/staging con fail-fast contra producción;
2. Supabase local/staging con Auth + migraciones;
3. seed determinista de usuarios/datos de prueba;
4. ejecución real de `auth.spec.ts` y casos cross-institución;
5. workflow manual/nightly con artifacts.

## Siguiente checkpoint

**044B — backend staging + Supabase local efímero**.

Prioridad: mantener el enfoque sin costo adicional usando Docker/Supabase CLI en
un entorno desechable, antes de considerar infraestructura cloud persistente.

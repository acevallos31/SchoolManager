# 044E — cierre de regresión E2E autenticada

Fecha: 2026-09-16
Rama: `feature/e2e-regresion-044e`

## Objetivo

Convertir los gates puntuales de 044C/044D en una regresión E2E mantenible y
reutilizable, sin depender de producción ni de infraestructura cloud adicional.

## Cambios

- se agrega `.github/workflows/e2e-regression.yml`;
- el workflow admite `workflow_dispatch`, `schedule` nocturno y `workflow_call`;
- para validar cambios del propio harness también corre en PR cuando cambian
  `.github/workflows/e2e-regression.yml`, `e2e/**`, `scripts/e2e/**` o `supabase/**`;
- ejecuta el stack local efímero completo: Supabase local, API .NET, Angular
  staging, seed de identidad/RBAC, seed académico multiinstitución y toda la
  suite Playwright;
- Playwright genera reporte HTML bajo CI además de trazas/screenshots;
- si el job falla, GitHub Actions sube `playwright-report` y `test-results` como
  artifact con retención de 14 días;
- el cleanup de staging local corre siempre;
- se retiran los workflows temporales específicos de 044C y 044D.

## Guardrails

La regresión conserva las barreras construidas en 044A–044D:

- Supabase de seed solo acepta loopback `:54321`;
- frontend/API/PostgreSQL de staging pasan validaciones anti-producción;
- no se versionan secretos ni credenciales;
- los usuarios E2E usan dominios `.test` y contraseñas efímeras;
- no se asigna `platform_admin` automáticamente;
- no hay migraciones ni escrituras sobre Supabase de producción;
- el artifact de fallo contiene únicamente salida de Playwright del dataset E2E,
  no el log crudo de arranque de Supabase.

## Programación nocturna

El cron `17 8 * * *` corre aproximadamente a las 02:17 de Honduras (UTC-6).
GitHub Actions usa UTC para `schedule`.

## Criterio de cierre

044E queda listo para merge únicamente cuando:

1. CI estándar y SonarCloud del PR estén verdes;
2. `E2E Authenticated Regression` pase completo sobre el HEAD final;
3. el cleanup termine correctamente;
4. no existan hallazgos de seguridad nuevos ni secretos versionados.

Al fusionarse 044E, la deuda técnica #13 puede moverse de abierta a resuelta.
Los pendientes reales posteriores quedan reducidos a observabilidad (#5) y
prueba de carga controlada del backend (#14 / issue #85).

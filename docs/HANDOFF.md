# HANDOFF — Matrículas Fase 1C (frontend)

> Nota: la sección 17 de `AGENTS.md` continúa siendo el handoff canónico, pero ese archivo está protegido contra escritura por agentes. Este documento es el handoff operativo de la presente sesión; consolidar en `AGENTS.md`/`AI_CONTEXT.md` cuando la persona responsable pueda editarlo.

## Fecha y hora

2026-09-04 00:20 -06:00 (UTC-6).

## Rama

`feature/matriculas-fase-1c-frontend`, creada desde `main` (`19c7f5f`).

## Objetivo

Implementar la Fase 1C de Matrículas en el frontend Angular sobre la API .NET ya integrada (Fase 17A, PR #26): servicio HTTP de matrículas, reescritura de la página `/matriculas`, botón "Matricular" en Alumnos, tests, revisión de checks externos y actualización de documentación.

## Estado

Completado. PR #28 abierto en `draft` y revisado; todos los checks en verde. Sin merge. Los cambios son frontend-only.

## Completado

- `core/services/matriculas.service.ts` (nuevo): consume `/api/matriculas` vía `HttpClient` — listar, crear y cambiar estado, con mapeo de errores (400/403/404/409). Patrón según `mensualidad.service.ts`.
- `pages/matriculas/*` (reescritos): tabla (alumno, ciclo, grado, sección, jornada, período, fecha, estado con badge), filtros por alumno/ciclo/estado, formulario de alta guiado (alumno + ciclo → sección y período, sin monto) y modal de cambio de estado con motivo obligatorio para estados terminales.
- `pages/alumnos/*`: getter `puedeMatricular` (permiso `academico.matriculas.crear`) y botón "Matricular" que navega a `/matriculas?alumnoId=...` con el alumno preseleccionado.
- Tests: `matriculas.service.spec.ts` (HttpTestingController) y `matriculas.spec.ts` (servicios mockeados, `ActivatedRoute`/`Router` provistos).

## Archivos

- Frontend (9): `core/services/matriculas.service.ts` (+spec), `pages/matriculas/matriculas.{ts,html,css,spec.ts}`, `pages/alumnos/alumnos.{ts,html,css}`.
- Docs: `docs/AI_CONTEXT.md` (estado frontend actualizado).

## Validaciones

- `npm run build`: correcto, sin errores (warnings preexistentes canvg/jspdf).
- `npx ng test --watch=false`: 17 archivos / 61 tests en verde.
- `git diff --check`: correcto.
- CI GitHub Actions: pass.
- SonarCloud: pass, 0 issues (se corrigió `Web:InputWithoutLabelCheck` con `id`/`for` en matriculas.html).
- Vercel: pass (preview); "Desplegar a Producción" `skipping` en draft.

## Commits / Git

- `921915e` feat(matriculas): conectar frontend Matriculas a la API de Fase 1C...
- `9a9ddc7` fix(matriculas): asociar labels con sus controles vía id/for (Sonar)
- Push: realizado a `origin/feature/matriculas-fase-1c-frontend`. PR #28 (draft). Merge: no realizado.

## Pendientes

- Aprobación humana del PR #28 y merge a `main`.
- Verificación E2E del flujo real frontend ↔ API con datos de prueba tras el merge (los tests usan mocks).

## Riesgos

- Contrato con la API no ejecutado contra una instancia en vivo; confirmar en preview tras el merge.

## Protección

La escritura automática sobre `AGENTS.md` está bloqueada (archivo de instrucciones del agente); el handoff canónico debe actualizarse manualmente por la persona responsable cuando lo estime.
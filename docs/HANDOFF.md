# HANDOFF — Matrículas Fase 1C (frontend)

## Fecha y hora

2026-09-04 07:00 -06:00 (UTC-6).

## Rama

`feature/matriculas-fase-1c-frontend`, creada desde `main` (`19c7f5f`).

## Objetivo

Implementar la Fase 1C de Matrículas en el frontend Angular sobre la API .NET ya integrada (Fase 17A, PR #26): servicio HTTP de matrículas, reescritura de la página `/matriculas`, botón "Matricular" en Alumnos, tests, revisión de checks externos y actualización de documentación.

## Estado

Completado a nivel de implementación. PR #28 abierto y Ready for review, sin merge. La revisión posterior reforzó tipado estricto y restringió las transiciones de estado del frontend al contrato permitido por backend.

## Completado

- `core/services/matriculas.service.ts`: consume `/api/matriculas` vía `HttpClient` — listar, crear y cambiar estado, con mapeo de errores (400/403/404/409).
- `pages/matriculas/*`: tabla con alumno, ciclo, grado, sección, jornada, período, fecha y estado; filtros por alumno/ciclo/estado; formulario de alta guiado (alumno + ciclo → sección y período, sin monto); modal de cambio de estado con motivo obligatorio para retirada/anulada/trasladada.
- Transiciones UI alineadas con backend: `pendiente -> activa|anulada`; `activa -> finalizada|retirada|anulada|trasladada`; estados terminales sin nuevas transiciones.
- `pages/alumnos/*`: getter `puedeMatricular` y botón "Matricular" para alumnos activos con permiso `academico.matriculas.crear`.
- Tests: `matriculas.service.spec.ts` con HttpTestingController y `matriculas.spec.ts` con servicios mockeados, incluyendo cobertura de transiciones.
- Accesibilidad: controles asociados a sus labels mediante `id`/`for`.
- `AGENTS.md`: autonomía nocturna aclarada, regla de salida truncada añadida y HANDOFF canónico actualizado.

## Archivos

- Frontend: `core/services/matriculas.service.ts` (+spec), `pages/matriculas/matriculas.{ts,html,css,spec.ts}`, `pages/alumnos/alumnos.{ts,html,css}`.
- Docs: `docs/AI_CONTEXT.md`, `docs/HANDOFF.md`, `AGENTS.md`.

## Validaciones

Antes de la revisión adicional:

- `npm run build`: correcto, sin errores (warnings preexistentes canvg/jspdf).
- `npx ng test --watch=false`: 17 archivos / 61 tests en verde.
- `git diff --check`: correcto.
- CI GitHub Actions: pass.
- SonarCloud: pass, 0 issues.
- Vercel: pass (preview).

Después de la revisión adicional se modificaron tipado y transiciones y se agregaron tests; por lo tanto los checks del HEAD actualizado del PR #28 deben volver a completarse antes del merge.

## Commits / Git

Commits originales de Hermes:

- `921915e` feat(matriculas): conectar frontend Matriculas a la API de Fase 1C...
- `9a9ddc7` fix(matriculas): asociar labels con sus controles vía id/for (Sonar)
- `dfb6305` docs: actualizar estado frontend + HANDOFF

Revisión posterior:

- tipado estricto de estados;
- limitación de transiciones válidas;
- cobertura de tests adicional;
- actualización de `AI_CONTEXT.md`, `HANDOFF.md` y `AGENTS.md`.

PR #28: Ready for review. Merge: no realizado.

## Pendientes

- Esperar CI/Sonar/Vercel del HEAD actualizado.
- Revisión humana final del PR #28 y merge a `main` si todo permanece verde.
- Verificación E2E del flujo real frontend ↔ API con datos de prueba controlados tras el merge.

## Riesgos

- El contrato frontend ↔ API no se ha ejecutado aún contra una instancia viva con datos de prueba; los tests del frontend usan mocks.

## Política de AGENTS.md

`AGENTS.md` es el handoff operativo canónico y puede/debe ser actualizado por agentes cuando la tarea autoriza documentación o handoff. Si el runtime de un agente protege ese archivo y exige aprobación, eso es una restricción de la herramienta, no una prohibición del repositorio.

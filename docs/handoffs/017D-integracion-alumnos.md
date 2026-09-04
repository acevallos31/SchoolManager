# HANDOFF — 017D Integración Alumnos → Matricular

## Agente, fecha y rama

- Agente: Hermes Agent (cron 2026-09-04).
- Fecha: 2026-09-04 11:57 -06:00 (UTC-6).
- Rama: `feature/matriculas-fase-1c-cierre` desde `main` (`2c5f866`).

## Objetivo

Cerrar el Bloque 017 Matrículas Fase 1C (17D Integración Alumnos → Matricular; 17E Tests; 17F Integración/cierre) tras el merge del PR #28 en `main`.

## GATE 0 (verificado)

- PR #28 `feat(matriculas): conectar frontend Matriculas a la API de Fase 1C` → MERGED, mergeCommit `2c5f866`, mergedAt 2026-09-04T17:49:37Z.
- `main` actualizado a `2c5f866`, working tree limpio (vía `git pull --ff-only`).
- Backend 17A presente (`19c7f5f`) y frontend 17C presente (`2c5f866`).

## 17D — Auditoría de integración Alumnos → Matricular

Se auditaron `pages/alumnos/alumnos.{ts,html}` y `pages/matriculas/matriculas.{ts,html}`. Resultado:

- Botón `Matricular` solo para alumnos activos y con permiso `academico.matriculas.crear` ✓.
- Alumno inactivo no ofrece la acción ✓.
- Navegación a `/matriculas?alumnoId=<id>` con `queryParams` ✓.
- Alumno preseleccionado al llegar (query param) ✓.
- El formulario no duplica reglas de negocio del backend (sin monto/estado `pagada`, sin logica transaccional en frontend) ✓.
- Errores de matrícula se muestran de forma segura (`mensaje` + `esError`) ✓.
- Ausencia completa del estado `cancelada`; estados tipados alineados al contrato backend (`pendiente/activa/finalizada/retirada/anulada/trasladada`) ✓.

No se reescribió código de integración: ya estaba correcto. Solo se añadieron tests de cobertura (17E).

## Archivos tocados en 17D

- Solo tests: `src/app/pages/alumnos/alumnos.spec.ts`.

## Pendiente 17D

Ninguno.
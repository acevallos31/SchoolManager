# HANDOFF — 017E Tests completos

## Agente, fecha y rama

- Agente: Hermes Agent (cron 2026-09-04).
- Fecha: 2026-09-04 11:57 -06:00 (UTC-6).
- Rama: `feature/matriculas-fase-1c-cierre` desde `main` (`2c5f866`).

## Objetivo

Revisar y completar la cobertura real de Matrículas en frontend, backend y database integration, agregando solo tests donde existía un hueco relevante.

## Frontend — Matrículas

Se revisó `pages/matriculas/matriculas.spec.ts` y se añadieron tests para cerrar huecos:

- **Estado vacío**: cuando el listado responde `[]` se muestra "No hay matrículas registradas".
- **Error del servicio**: cuando el listado lanza, queda `mensaje` seguro ("No se pudieron cargar las matrículas") y `esError=true`.
- **Preselección por query param**: `?alumnoId=a1` preselecciona el alumno en `nueva` y en `filtros`, y abre el formulario (solo con permiso `crear`).
- **Sin permiso crear**: el query param no preselecciona ni abre el formulario.
- **Limpieza de filtro obsoleto** al recargar.

Ya cubiertos en baseline (no duplicados): listado, filtros alumno/ciclo/estado, creación válida, campos obligatorios, 409 duplicada, permisos ver/crear/cambiar_estado, transiciones válidas, transiciones inválidas no ofrecidas, motivo obligatorio, ausencia de `cancelada`, loading.

## Frontend — Servicio HTTP

`core/services/matriculas.service.spec.ts` ya cubría GET/POST/PUT estado y errores 400/403/404/409/500 (verificado, no se duplicó).

## Backend / API

Se revisaron y ejecutaron los tests existentes de `SchoolManager.API.IntegrationTests` (`dotnet test -c Release`): 28 tests en verde. No se duplicaron tests ya cubiertos.

## DB

Se revisó la cobertura existente y se ejecutó `SchoolManager.Database.IntegrationTests -c Release` (Testcontainers, Docker disponible): 80 tests en verde. No se modificó ninguna migración para hacer pasar tests.

## Resultado de cobertura

Estado de lanzamiento global del frontend: **17 archivos / 71 tests en verde** (antes 17/63). Todos los items de cobertura mínima del 17E quedan contemplados.

## Pendiente 17E

Ninguno.
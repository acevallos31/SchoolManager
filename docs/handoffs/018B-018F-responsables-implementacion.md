# 018B–018F — Implementación y validación del módulo Responsables

Estado: COMPLETADA (2026-09-04). Rama `feature/responsables-fase-018` sobre `main` (`35b95e0`, incluye PR #29 y #30). NO mergeado — PR listo para revisión.

Reportado a mano: AGENTS.md §17 está bloqueado contra escritura por config de Hermes; el canon queda en `docs/AI_CONTEXT.md` + este handoff.

## 018B — Migración 017 (RPCs de gestión de responsables)

`database/migrations/017_responsables_gestion_rpc.sql` (+ `validation/017_...validation.sql`, `rollback/017_...rollback.sql`)

- 9 funciones base (SECURITY INVOKER) + 9 wrappers RPC `security definer`, patrón 008/009/011:
  - `rpc_crear_responsable_con_documento` / `rpc_crear_responsable_para_persona`
  - `rpc_editar_responsable`
  - `rpc_inactivar_responsable` / `rpc_reactivar_responsable`
  - `rpc_vincular_alumno_responsable` / `rpc_editar_vinculo_responsable`
  - `rpc_desactivar_vinculo_responsable` / `rpc_reactivar_vinculo_responsable`
- Invariantes en DB: persona+institución único (`uq_responsables_persona_institucion`, 23505), un solo principal activo por alumno (índice parcial, 23514), no-cruce de institución (23514), sin DELETE físico (soft con motivo).
- Superficie solo-RPC: sin policies INSERT/UPDATE directas; RLS sigue protegiendo las tablas.
- Grants mínimos bajo `academico.responsables.*` (namespace vigente; `responsables.responsables.*` de 007 es legacy sin uso, conservado).
- Baseline `001_schoolmanager_fase1a.sql` anexado (append manual del bloque 017); validado en BD limpia.

## 018C — Backend

- `backend/SchoolManager.API/DTOs/ResponsableDto.cs` (ResponsableDto + ResponsableVinculoDto + DTOs de request).
- `backend/SchoolManager.API/Authorization/Permisos.cs` → clase `Responsables` (`ver/crear/editar`).
- `backend/SchoolManager.API/Controllers/ResponsablesController.cs` (461 L), patrón MatriculasController:
  - GET `api/responsables` (paginado `PaginatedResult`, filtros `institucionId/termino/estado/page/pageSize`), GET por id, GET `alumno/{alumnoId}` (vínculos).
  - POST crear, POST `para-persona`; PUT editar, PUT `{id}/estado` (desactivar); POST `{id}/reactivar`.
  - POST `alumno/{alumnoId}` (vincular), PUT `vinculo/{vinculoId}`, PUT `.../desactivar`, POST `.../reactivar`.
  - `ToError` mapea 42501→403, P0002→404, 23505/23514→409, 22023/23503/SM001/SM003→400.
- **Bug real corregido (lo cazaron los tests de API, no los de DB)**: parámetros nullable (`telefono/correo/parentesco`) se pasaban como `null` a `AddWithValue` → Npgsql lanzaba `InvalidOperationException` (500). Corregido con `?? DBNull.Value` (patrón MatriculasController). Los tests DB pasaban porque invocan el RPC directo, sin pasar por el controller.
- Build Release: **0 errores / 0 warnings**.

## 018D — Frontend

- `core/services/responsables.service.ts` — consume la API .NET (`matriculas.service.ts` como base): listar paginado, CRUD responsable, estado, vínculos por alumno, vincular/editar/desvincular/re-vincular, mapeo de errores 400/403/404/409.
- `pages/responsables/` — página `/responsables` (standalone): listado paginado server-side + filtros (término/estado), formulario crear/editar, desactivar (prompt motivo) / reactivar, permisos `academico.responsables.{ver,crear,editar}`.
- **Integración Alumno→Responsable**: botón "Responsables" en `pages/alumnos` → `responsables?alumnoId=...`; la página muestra el panel `Vínculos del alumno` (parentesco, principal, acceso financiero, estado, desactivar vínculo).
- Rutas en `app.routes.ts`; enlace en menú `dashboard`.
- Compila **0 errores** (`tsc -p tsconfig.app.json`). Suite vitest completa **87/87** (+14 nuevos).

## 018F — Tests

- `tests/SchoolManager.Database.IntegrationTests/Tests/ResponsablesGestionTests.cs` (9 tests DB): crear, duplicado 23505, reutilizar persona, sin permiso (403), principal único, cruce institución 23514, desactivar/reactivar soft, editar, desactivación en cascada de vínculos. Parámetros posicionales `AddWithValue(value)` (SQL `$1`); conteos filtrados por `institucion_id` (fixture compartida); documento normalizado como la DB (minúsculas + letras/dígitos).
- `MigrationTests.cs`: rango de migraciones actualizado de **001-016 → 001-017**. Suite DB completa **89/89**.
- `tests/SchoolManager.API.IntegrationTests/ResponsablesControllerTests.cs` (**13 tests API**): CRUD, duplicado 409, datos inválidos 400, sin permiso 403, 404, desactivar/reactivar, vincular + listar vínculos, desactivar vínculo, aislamiento multi-institución, paginación, multi-sin-institución 400 (no 500). Requiere permisos `academico.responsables.*` en AdminA/AdminB (añadidos a `MatriculasApiFactory`). Suite API completa **46/46**.
- Frontend: `responsables.spec.ts` **14 tests** (permiso, listado, filtros, crear/editar, desactivar/reactivar, paginación, vínculos de alumno). Suite completa **87/87**.

## Gates de calidad
- Tests DB: 89/89. Tests API: 46/46. Tests frontend: 87/87.
- Build backend Release: 0 errores. Compilación frontend: 0 errores.
- Migración 017 validada funcionalmente en Postgres 16 desechable; baseline aplica limpio.
- Sin merge a main. Archivos sin commitear (estado en branch).

## Pendientes
- **018G auditoría Supabase read-only**: no ejecutada — requiere credenciales/URL de un lector remoto que no están en sesión. Se documenta como pendiente con acceso apropiado.
- PR: crear/pushear la rama y abrir PR a main con CI/Sonar/Vercel (no bloqueado aquí).
- Legacy documentado: namespace `responsables.responsables.*` (007) duplicado, `Alumno.cs` `PadreId/TutorId`, `portal-padre` con `tutor_id` inexistente.
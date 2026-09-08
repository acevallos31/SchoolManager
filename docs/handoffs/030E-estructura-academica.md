# Handoff 030E — Estructura Académica: migración a API .NET

Bloque 030E del Plan de Migración API (deuda técnica #10). Rama `feature/arquitectura-api-030`, PR #53 abierto contra `main` — **sin mergear hasta cerrar 030F (cierre)**.

## Estado
- **Verde completo.** Backend de estructura 18/18, suite DB 151/151, suite frontend 211/211, builds backend y frontend OK, `git diff --check` limpio, working tree limpio.
- HEAD actual: `36e9d47` (4 commits 030E sobre `b78524a` = línea base 030D).

## Objetivo cumplido
`estructura-academica.service.ts` dejó de acceder directamente a Supabase y ahora consume la API .NET (`/api/estructura-academica`) vía `HttpClient` contra `environment.apiUrl`. Se preservan interfaces/shapes (`Grado`, `Jornada`, `Seccion`, `GradoInput`, `JornadaInput`, `SeccionInput`), la clase `EstructuraAcademicaError(message, code)` y las **15 firmas públicas idénticas** (todas conservan `institucionId?`), de modo que los callers (`configuracion-estructura-academica`, `matriculas`) no cambian de contrato.

## Endpoints creados (`EstructuraAcademicaController`, ruta `api/estructura-academica`)
| # | Método | Ruta | Permiso .NET | RPC 016 delegada |
|---|---|---|---|---|
| E1 | GET | `api/estructura-academica/grados?institucionId=` | `academico.estructura.ver` | `rpc_listar_grados` |
| E2 | POST | `api/estructura-academica/grados` `{nombre, orden, institucionId?}` | `academico.estructura.editar` | `rpc_crear_grado` |
| E3 | PUT | `api/estructura-academica/grados/{id:guid}?institucionId=` `{nombre, orden}` | `academico.estructura.editar` | `rpc_actualizar_grado` |
| E4 | POST | `api/estructura-academica/grados/{id:guid}/desactivar` | `academico.estructura.desactivar` | `rpc_desactivar_grado` |
| E5 | POST | `api/estructura-academica/grados/{id:guid}/reactivar` | `academico.estructura.editar` | `rpc_reactivar_grado` |
| E6 | GET | `api/estructura-academica/jornadas?institucionId=` | `academico.estructura.ver` | `rpc_listar_jornadas` |
| E7 | POST | `api/estructura-academica/jornadas` `{nombre, institucionId?}` | `academico.estructura.editar` | `rpc_crear_jornada` |
| E8 | PUT | `api/estructura-academica/jornadas/{id:guid}?institucionId=` `{nombre}` | `academico.estructura.editar` | `rpc_actualizar_jornada` |
| E9 | POST | `api/estructura-academica/jornadas/{id:guid}/desactivar` | `academico.estructura.desactivar` | `rpc_desactivar_jornada` |
| E10 | POST | `api/estructura-academica/jornadas/{id:guid}/reactivar` | `academico.estructura.editar` | `rpc_reactivar_jornada` |
| E11 | GET | `api/estructura-academica/secciones?cicloId=&institucionId=` | `academico.estructura.ver` | `rpc_listar_secciones` |
| E12 | POST | `api/estructura-academica/secciones` `{cicloId, gradoId, jornadaId?, nombre, cupo?, institucionId?}` | `academico.estructura.editar` | `rpc_crear_seccion` |
| E13 | PUT | `api/estructura-academica/secciones/{id:guid}?institucionId=` `{cicloId, gradoId, jornadaId?, nombre, cupo?}` | `academico.estructura.editar` | `rpc_actualizar_seccion` |
| E14 | POST | `api/estructura-academica/secciones/{id:guid}/desactivar` `{motivo}` | `academico.estructura.desactivar` | `rpc_desactivar_seccion` |
| E15 | POST | `api/estructura-academica/secciones/{id:guid}/reactivar` | `academico.estructura.editar` | `rpc_reactivar_seccion` |

## RPC mapeadas (sin reimplementar reglas)
Todos los endpoints delegan en las RPC de la migración **016**, que siguen siendo la fuente de invariantes (duplicados, rango, matrícula asociada, contexto institucional). El controller NO reimplementa SQL/RPC ni reglas de negocio en C#; solo orquesta las llamadas y lee los `returns table` por índice.
- Grados: `rpc_listar/crear/actualizar_grado`; desactivar/reactivar por `rpc_desactivar_grado`/`rpc_reactivar_grado`.
- Jornadas: `rpc_listar/crear/actualizar_jornada`; `rpc_desactivar_jornada`/`rpc_reactivar_jornada`.
- Secciones: `rpc_listar/crear/actualizar_seccion`; `rpc_desactivar_seccion` (**única que exige `motivo`**)/`rpc_reactivar_seccion`.

## Permisos aplicados (doble capa)
- **Capa de aplicación .NET** (policy): `academico.estructura.ver/editar/desactivar` — los 3 aprobados. Solo existen esos tres: **crear y reactivar se mapean a `editar`** (no existe `academico.estructura.crear`). Aplicados con `[Authorize(Policy=...)]` ANTES de invocar DB/RPC.
- **Capa interna DB** (invariante, intacta): las RPC 016 validan `configuracion.grados.*`, `configuracion.jornadas.*`, `configuracion.secciones.*` vía `usuario_tiene_permiso_actual`. No se tocó.
- **Migración RBAC 024** (nueva): registra `academico.estructura.ver/editar/desactivar` en el catálogo y los otorga a `admin` (paridad con 016). Era necesaria: esos permisos NO existían en el catálogo DB (solo en `Permisos.cs` .NET). Aditiva e idempotente. Tiene validación y rollback (este último desregistra la versión en `schema_migrations`).
- Isolation institucional: las RPC 016 resuelven institución vía `resolver_institucion_operacion`/contexto; el controller no rompe ese mecanismo. El factory de tests es **mono-institucional** (mismo patrón que ciclos 030D).

## Tests
- **Backend integración** `EstructuraAcademicaControllerTests` (18 casos): listar/crear/actualizar/desactivar/reactivar grado, jornada y sección; 400 nombre en blanco; 400 sección sin `cicloId`; 400 sección sin motivo de desactivación; 404 recurso inexistente; 401 sin sesión; 403 sin permiso. Factory `EstructuraAcademicaApiFactory` (mono-institución, siembra los 3 permisos `academico.estructura.*` + filas base grado/jornada/sección/ciclo).
- **Suite DB** 151/151: incluye validación de la 024 y el snapshot `MigrationTests` actualizado (001→024).
- **Frontend**: spec del service migrado (12 casos: GET/POST/PUT + mapeo 403 a `EstructuraAcademicaError`). Suite completa 211/211 (25 archivos) — incluye specs de `configuracion-estructura-academica` y `matriculas` que consumen el servicio, verificando que los callers no se rompieron.

## Builds
- `dotnet build backend/SchoolManager.API` → 0 warn, 0 err.
- `dotnet build tests/SchoolManager.API.IntegrationTests` → 0 warn, 0 err.
- `dotnet build tests/SchoolManager.Database.IntegrationTests` → 0 warn, 0 err.
- `npx ng build` → OK.
- `git diff --check` → limpio.

## Accesos Supabase restantes en Estructura
- `estructura-academica.service.ts`: **0 matches** de `SUPABASE_CLIENT`, `.from(`, `.rpc(`, `createClient`. Migrado (criterio tarea 15 cumplido).

## Commits (rama `feature/arquitectura-api-030`, sobre `b78524a`)
- `43bb35c` Migración RBAC 024 (030E): catálogo academico.estructura.* + rol admin
- `8469125` Backend (030E): EstructuraAcademicaController + DTOs (15 ops E1-E15)
- `f81c5a1` Tests (030E): factory + 18 tests de integración EstructuraAcademica
- `36e9d47` Frontend (030E): estructura-academica.service migrado a HttpClient (sin Supabase) + spec

## Archivos clave
- `backend/SchoolManager.API/Controllers/EstructuraAcademicaController.cs` (nuevo)
- `backend/SchoolManager.API/DTOs/EstructuraAcademicaDto.cs` (nuevo)
- `database/migrations/024_rbac_permisos_aplicacion_estructura.sql` + `validation/` + `rollback/`
- `tests/SchoolManager.API.IntegrationTests/EstructuraAcademicaControllerTests.cs` + `Infrastructure/EstructuraAcademicaApiFactory.cs`
- `tests/SchoolManager.Database.IntegrationTests/Tests/MigrationTests.cs` (snapshot 024)
- `frontend/.../core/services/estructura-academica.service.ts` + `.spec.ts`

## Riesgos residuales
1. **Do not merge** PR #53 hasta cerrar 030F (cierre).
2. 030E migró Estructura (Grados/Jornadas/Secciones); **queda 030F (cierre)** del Bloque 030 y luego el merge.
3. Solo existen permisos `academico.estructura.ver/editar/desactivar` (aprobados). No hay `crear`; crear/reactivar usan `editar`. Si en el futuro se quisiera granularidad, habría que agregar permisos y capa en la 016 — fuera de alcance.
4. El factory API de estructura es mono-institución (como ciclos 030D), a diferencia del multi de Matriculas. Coherente con cómo las RPC 016 resuelven institución.
5. La página `configuracion-estructura-academica.ts` y `matriculas.ts` consumen el servicio migrado y sus specs pasan sin cambios de contrato; se re-verificaron en 030E.

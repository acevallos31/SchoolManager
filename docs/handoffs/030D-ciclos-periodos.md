# Handoff 030D — Ciclos/Períodos: migración a API .NET

Bloque 030D del Plan de Migración API (deuda técnica #10). Rama `feature/arquitectura-api-030`, PR #53 abierto contra `main` — **sin mergear hasta cerrar todo el Bloque 030**.

## Estado
- **Verde completo.** Backend del controller 13/13, suite DB 151/151, frontend service/páginas + suite completa 200/200, builds backend y frontend OK.
- HEAD actual: `3654629` (commit fix rollback). Trabajo sobre `c5897ad`.

## Objetivo cumplido
`ciclo-escolar.service.ts` dejó de acceder directamente a Supabase y ahora consume la API .NET (`/api/ciclos-escolares`) vía `HttpClient` contra `environment.apiUrl`. Se preservan shapes/interfaces y `CicloEscolarError(message, code)`; los callers (`configuracion-ciclos`, `configuracion-estructura-academica`) no cambian de contrato, salvo un ajuste interno de firma en desactivar/reactivar período (ahora reciben `cicloId` para la ruta anidada).

## Endpoints creados (`CiclosEscolaresController`, ruta `api/ciclos-escolares`)
| Método | Ruta | Permiso .NET |
|---|---|---|
| GET | `api/ciclos-escolares` | `academico.ciclos.ver` |
| POST | `api/ciclos-escolares` | `academico.ciclos.crear` |
| PUT | `api/ciclos-escolares/{id}` | `academico.ciclos.editar` |
| POST | `api/ciclos-escolares/{id}/desactivar` | `academico.ciclos.desactivar` |
| POST | `api/ciclos-escolares/{id}/reactivar` | `academico.ciclos.editar` |
| GET | `api/ciclos-escolares/{id}/periodos` | `academico.ciclos.ver` |
| POST | `api/ciclos-escolares/{id}/periodos` | `academico.ciclos.crear` |
| PUT | `api/ciclos-escolares/{id}/periodos/{periodoId}` | `academico.ciclos.editar` |
| POST | `api/ciclos-escolares/{id}/periodos/{periodoId}/desactivar` | `academico.ciclos.desactivar` |
| POST | `api/ciclos-escolares/{id}/periodos/{periodoId}/reactivar` | `academico.ciclos.editar` |

## RPC mapeadas (sin reimplementar reglas)
Los endpoints delegan en las RPC existentes de la migración 014 (y 015 para períodos anticipados), que quedan como **fuente de invariantes**:
- `rpc_listar_ciclos_escolares`, `rpc_crear_ciclo_escolar`, `rpc_actualizar_ciclo_escolar`, `rpc_desactivar_ciclo_escolar` (+ reactivar vía `rpc_actualizar` con `activo=true`).
- `rpc_listar_periodos_matricula`, `rpc_crear_periodo_matricula`, `rpc_actualizar_periodo_matricula`, `rpc_desactivar_periodo_matricula` (+ reactivar análogo).
- El controller NO reimplementa SQL/RPC ni reglas de negocio en C#; solo orquesta llamadas y lee el estado `activo` actual (para preservarlo en `actualizar`) mediante query directa acotada por id de fila.

## Permisos aplicados (doble capa)
- **Capa de aplicación .NET** (policy): `academico.ciclos.ver/crear/editar/desactivar` — los 4 aprobados. Aplicados con `[Authorize(Policy=...)]` ANTES de invocar DB/RPC.
- **Capa interna DB** (invariante, intacta): las RPC 014 validan `configuracion.ciclos.*` y `configuracion.periodos_matricula.*` vía `usuario_tiene_permiso_actual`. No se tocó.
- **Migración RBAC 023** (nueva): registra `academico.ciclos.*` en el catálogo y los otorga a `admin` (paridad con 014). Sin ella, ningún usuario real pasaría la policy .NET en producción porque `academico.ciclos.*` no existía en el catálogo DB. Aditiva e idempotente. Tiene validación y rollback (este último desregistra la versión en `schema_migrations`).
- Isolation institucional: los RPC 014 resuelven institución vía `resolver_institucion_operacion`/`resolver_contexto_institucional`; el controller no rompe ese mecanismo. El factory de tests es **mono-institucional** porque los RPC de períodos llaman `resolver(null)` (lanzan SM003 en modo multi).

## Tests
- **Backend integración** `CiclosEscolaresControllerTests` (13 casos): listar/crear/actualizar/desactivar/reactivar ciclo y período; 401 sin sesión; 403 sin permiso; 404 ciclo inexistente; 409 duplicado de nombre; 400 rango invertido de período. Factory `CiclosEscolaresApiFactory` (mono-institución, siembra los 4 permisos `academico.ciclos.*`).
- **Suite DB** 151/151: incluye validación de la 023 y el snapshot `MigrationTests` actualizado (001→023).
- **Frontend**: spec del service migrado (12 casos: GET/POST/PUT + mapeo 400/403 a `CicloEscolarError`), specs de `configuracion-ciclos` (6) y `configuracion-estructura-academica` (3). Suite completa 200/200.

## Builds
- `dotnet build backend/SchoolManager.API` → 0 warn, 0 err.
- `ng build --configuration=production` → OK.
- `git diff --check` → limpio.

## Accesos Supabase restantes en Ciclos
- `ciclo-escolar.service.ts`: **0 matches** de `SUPABASE_CLIENT`, `.from(`, `.rpc(`, `createClient`, `supabase` (solo una mención en comentario). Migrado.

## Commits (push a `feature/arquitectura-api-030`)
- `97a5421` feat(ciclos): controller API 030D
- `412b00f` feat(db): migración 023 RBAC academico.ciclos.*
- `42d7259` test(ciclos): integración API 030D (13 casos)
- `d9f0c4a` feat(frontend): ciclo-escolar.service a HttpClient
- `3654629` fix(db): rollback 023 desregistra versión + snapshot MigrationTests

## Archivos clave
- `backend/SchoolManager.API/Controllers/CiclosEscolaresController.cs` (nuevo)
- `backend/SchoolManager.API/DTOs/CicloEscolarDto.cs` (nuevo)
- `database/migrations/023_rbac_permisos_aplicacion_ciclos.sql` + `validation/` + `rollback/`
- `tests/SchoolManager.API.IntegrationTests/CiclosEscolaresControllerTests.cs` + `Infrastructure/CiclosEscolaresApiFactory.cs`
- `tests/SchoolManager.Database.IntegrationTests/Tests/MigrationTests.cs` (snapshot 023)
- `frontend/.../core/services/ciclo-escolar.service.ts` + `.spec.ts`
- `frontend/.../pages/configuracion-ciclos/configuracion-ciclos.ts` (+ `.spec.ts`)

## Riesgos residuales
1. **Do not merge** PR #53 hasta cerrar 030E (Estructura) y 030F (cierre).
2. 030D migró Ciclos/Períodos; **queda 030E Estructura** (`estructura-academica.service`, permisos `academico.estructura.ver/editar/desactivar` ya aprobados) y luego 030F.
3. La página `configuracion-estructura-academica.ts` consume `ciclo-escolar.service` para listar ciclos/períodos; su spec pasa, pero será re-verificada en 030E.
4. Los RPC de períodos resuelven institución con `resolver(null)` → en modo multi-institucional real los endpoints de períodos dependen del contexto institucional que ya existía (no es regresión: replica el comportamiento de la RPC 014).
5. El factory API de ciclos es mono-institución a diferencia del multi de Matriculas: si más adelante se quiere probar ciclos en multi, habrá que parametrizar.

# 030B — Alumnos: migración de acceso directo Supabase → API .NET

**Estado:** CERRADO (verde). Rama `feature/arquitectura-api-030`. No mergeado (Bloque 030 pendiente de 030C–030F).

**Alcance:** eliminar la deuda #10 (parcial) migrando el módulo de Alumnos del frontend para que deje de consultar Supabase directamente y pase por la API .NET. Sin reimplementar lógica SQL/RPC en C#: las escrituras delegan en las RPC existentes y las lecturas reutilizan `usuario_tiene_permiso_actual`/`resolver_institucion_operacion` (SECURITY DEFINER ya existentes). No se cambió modelo de datos ni se añadió funcionalidad.

## Backend (.NET, net10.0)
- `Authorization/Permisos.cs`: se añadieron `CiclosEscolares` (`academico.ciclos.*`) y `EstructuraAcademica` (`academico.estructura.ver/editar/desactivar`), registrados en `Permisos.Todos`. `Permisos.Alumnos` (`academico.alumnos.ver/crear/editar/desactivar`) ya existía.
- `DTOs/AlumnoDto.cs` (nuevo): `AlumnoDto` (con JOIN a persona y matrícula activa opcional), `MatriculaActualAlumnoDto`, `CrearAlumnoDto`.
- `Controllers/AlumnosController.cs` (nuevo, patrón `ResponsablesController`):
  - `GET /api/alumnos` → listado completo o búsqueda paginada (`termino/estado/page/pageSize`, `PaginatedResult`). Lectura SQL directa filtrada por `a.institucion_id = resolver_institucion_operacion(...)` **y** `usuario_tiene_permiso_actual('academico.alumnos.ver', …)`.
  - `GET /api/alumnos/{id:guid}` → 200 o 404 (filtrado por contexto institucional).
  - `POST /api/alumnos` → `rpc_crear_alumno_nueva_persona_con_documento`; 201 + `{ id }`; validación previa de campos obligatorios.
  - `POST /api/alumnos/{id:guid}/desactivar` → `rpc_desactivar_alumno`; 204. Motivo obligatorio.
  - `POST /api/alumnos/{id:guid}/reactivar` → `rpc_reactivar_alumno`; 204.
  - Error SQL → `ToError` (42501→403, P0002→404, 23505/23514→409, 22023/23503/SM001/SM003→400).
- La autorización .NET (policies de `Permisos.Alumnos.*`) se valida **antes** de invocar la RPC; RLS/DB queda como segunda capa.

## Frontend (Angular)
- `core/services/alumno.service.ts`: reescrito de Supabase → `HttpClient`/API .NET. Métodos **promise-returning** vía RxJS `.toPromise()` (convención del repo: `pagos.service`, `configuracion-financiera.service`). Firma preservada → **cero cambios en callers** (`alumnos.ts`, `matriculas.ts`). `AlumnoServiceError` pasa de `code:string` (SQLSTATE) a `status:number` (HTTP). 404 en detalle → `null`.
  - `listar()`, `buscarPaginado()`, `obtenerPorId()`, `crear()`, `desactivar()`, `reactivar()`.
- Specs: `alumno.service.spec.ts` reescrito a `HttpTestingController` (12 casos); `alumnos.spec.ts` actualizado a constructor de error con status numérico.

## Regresión
- `alumno.service.ts` sin `SUPABASE_CLIENT`, `.from(`, `.rpc(`, `createClient` (0 matches).

## Tests
- Frontend completo: **25 archivos / 189 tests** (antes 184). Build `ng build` OK.
- Backend build: **0 warnings / 0 errors**.
- Integración backend (`tests/SchoolManager.API.IntegrationTests`): **96 tests** verdes. Nuevos `AlumnosControllerTests` (12) cubren: listado autorizado, búsqueda paginada, detalle existente, 404, crear (201), crear duplicado (409), desactivar (204 + inactivo), desactivar sin motivo (400), reactivar (204 + activo), 401 sin autenticación, 403 sin permiso, aislamiento multiinstitucional (A no ve B; B sí ve lo suyo).
- Cambio en el fixture compartido: se concedieron `academico.alumnos.crear/editar/desactivar` a AdminA/B en `UsuarioActualControlado` de `MatriculasApiFactory` (aditivo; no rompe Matriculas/Responsables).

## Siguiente (Bloque 030)
- 030C Matrículas, 030D Ciclos/Períodos, 030E Estructura Académica, 030F cierre + Sonar + decisión de `configuracion.service` (diferida a 030F). No avanzar hasta dejar 030B aprobado.

# 030C — Matrículas: checkpoint de verificación (ya migrado desde Bloque 022)

**Estado:** CERRADO — **sin cambios funcionales** (checkpoint de verificación). Rama `feature/arquitectura-api-030`, sobre `0ed3031` (030B cerrado, PR #53 OPEN). No mergeado.

## Resultado principal
`matriculas.service.ts` fue migrado a API .NET en el **Bloque 022**; `MatriculasController` está completo. Por tanto **030C no requirió reimplementación ni endpoints nuevos ni cambios de código**; fue una verificación que quedó verde.

## Verificaciones realizadas

### 1. Accesos directos Supabase en el dominio Matrículas: **0**
Grep `SUPABASE_CLIENT|\.from\(|\.rpc\(|createClient|supabase` en:
- `core/services/matriculas.service.ts` → **0 matches**
- `pages/matriculas/` → **0 matches**

El servicio usa `HttpClient` con `baseUrl = ${environment.apiUrl}/matriculas`. Nota de estilo (NO corregida — 030C no refactoriza por estética): este servicio sigue devolviendo **Observables** (`.subscribe()` en página), a diferencia de `alumno.service` migrado en 030B que devuelve **Promesas** (`.toPromise()`). Es una divergencia de convención, no un acceso directo; queda como deuda cosmética menor, no bloqueante.

### 2. Mapeo operaciones frontend → endpoints (`MatriculasController`, ruta `api/matriculas`)
| Método servicio | Endpoint | Policy |
|---|---|---|
| `listar(alumnoId?)` | `GET /matriculas?alumnoId=` → `GetAll` (lista) | `Matriculas.Ver` |
| `listarPaginado(filtro)` | `GET /matriculas` (page/pageSize/cicloId/estado) → `GetAll` (paginado) — PERF-02 | `Matriculas.Ver` |
| `crear(input)` | `POST /matriculas` → `Create` | `Matriculas.Crear` |
| `cambiarEstado(id, cambio)` | `PUT /matriculas/{id}/estado` → `ActualizarEstado` | `Matriculas.CambiarEstado` |

- Todos los métodos usados por `pages/matriculas/matriculas.ts` (`listar`, `crear`, `cambiarEstado`) tienen contrato API.
- `listarPaginado` no tiene caller de página (solo spec) — análogo a `buscarPaginado` de Alumnos; contrato existe, sin endpoint nuevo requerido.
- Endpoints `POST /matriculas/registrar`, `GET /matriculas/alumno/{id}` existen en el controller sin caller frontend actual (legacy/API pública); no se eliminan.

### 3. Reglas de negocio/transiciones NO duplicadas en C#
- **Create** → `rpc_matricular_alumno(@a,@s,@pm)` (security definer). C# solo valida `AlumnoId/SeccionId/PeriodoMatriculaId` no vacíos.
- **ActualizarEstado** → `rpc_cambiar_estado_matricula` → función interna `cambiar_estado_matricula` (DB). Transiciones, mensualidades y permisos en DB.
- **Mensualidades/materialización de cargos**: RPC DB `rpc_generar_cargos_matricula` (migración `019_cargos_mensualidades_obligaciones`), no disparada por la transición aquí ni implementada en C#.
- Errores de RPC mapeados vía `ToError` en `ApiControllerBase` (42501→403, 23505→409, P0002→404, 22023→400). Sin reimplementación de SQL/RPC en C#.

### 4. Autorización y aislamiento institucional (patrón consistente con 030B)
- `Permisos.Matriculas.{Ver,Crear,CambiarEstado}` = `academico.matriculas.{ver,crear,cambiar_estado}` — **preexistentes desde 022**, dentro del esquema aprobado. **No se añadieron permisos nuevos.**
- Lecturas (`GetAll`, `GetById`, `GetMatriculasAlumno`): SQL con `FiltroContextoInstitucional` = `usuario_tiene_permiso_actual('academico.matriculas.ver', m.institucion_id)`; `GetAll` además `resolver_institucion_operacion(@institucionId)`. Auth de aplicación antes del acceso; RLS/DB = segunda capa.
- Sin identidad → 401 vía `TestAuthHandler` de la factory.

### 5. Tests ejecutados — todos verdes tras 030B
- Backend integración `MatriculasControllerTests` → **12/12 passed** (Testcontainers/Postgres, Docker).
- Frontend `matriculas.service.spec.ts` + `matriculas.spec.ts` → **2 archivos, 23/23 passed**.
- Suite completa ya verde en 030B (96 backend / 189 frontend) no se re-corrió íntegra; el cambio 030C es cero código.

## Archivos modificados en 030C
- **Ninguno de código.** Solo este handoff documental (`docs/handoffs/030C-matriculas.md`).

## Commit
- (tras handoff) documental 030C.

## Deuda residual
1. Divergencia de convención Observable vs Promise en `matriculas.service` frente a `alumno.service` (cosmética; migrar a Promise tocaría la página y viola «no refactorizar por estética» en 030C). Decision futuro: normalizar si se toca el servicio por otra razón.
2. `listarPaginado` sin caller de página (solo spec) — latente, análogo a 030B.
3. Endpoints `POST /registrar` y `GET /alumno/{id}` en controller sin caller frontend — legacy, no bloquea.
4. No se ejecutó suite completa integración tras 030C (cero cambios de código); de querer garantía total, correr 96 backend + 189 frontend.

## Pendiente del bloque
- 030D (ciclo-escolar) → 030E (estructura-academica) → 030F (configuracion/auth). **No avanzar hasta reporte y orden.**

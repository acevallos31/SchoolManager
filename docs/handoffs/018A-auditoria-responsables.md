# 018A — Auditoría del módulo Responsables

Estado: COMPLETADA (2026-09-04). Rama `feature/responsables-fase-018` sobre `main` (incluye PR #29 y #30 mergeados).

## Gate 0
- PR #29: **MERGED**. PR #30: **MERGED** (ambos previos, ya en `main@`). Gate satisfecho.
- Working tree limpio; rama creada `feature/responsables-fase-018`.
- No hay otra ejecución de Hermes activa sobre SchoolManager.

## Mapa real (evidencia)

### Existe y es correcto
- **MIG004** — tablas:
  - `responsables`: `persona_id` + `institucion_id` con `uq_responsables_persona_institucion`, `estado in (activo,inactivo)`.
  - `alumno_responsable`: N:M con `uq_alumno_responsable (alumno_id,responsable_id)`, `es_principal`, `acceso_financiero`, `parentesco`, estado.
  - Invariante de **un solo principal activo por alumno**: índice parcial `ux_alumno_responsable_principal_activo on (alumno_id) where es_principal and estado='activo'`.
- **MIG007** — RBAC: roles `admin,operador,usuario,padre,docente,cajero,consulta`; primitivas `usuario_tiene_permiso`; permisos canónicos bajo `academico.*` y `identidad.*`.
- **MIG009** — RLS + grants mínimos:
  - `enable row level security` en `responsables` y `alumno_responsable`.
  - Policy `responsables_select` → `usuario_puede_ver_responsable(id)`.
  - Policy `alumno_responsable_select` → `usuario_puede_ver_alumno(alumno_id)`.
  - `revoke all on all tables` de anon/authenticated; luego `grant select ... to authenticated` (sin INSERT/UPDATE directo).
  - `usuario_puede_ver_alumno` ya contempla el acceso de un **padre** (join responsable↔alumno_responsable por `persona_id` del usuario).
  - Permisos `academico.responsables.ver/crear/editar` asignados a `operador` (> cia de 007 con `responsables.responsables.*`).
- **Tests DB previos**: `SchemaConstraintsTests` (Relacion_duplicada_rechazada, Solo_un_principal), `RlsSecurityTests` (padre ve su representado), `BaselineInstallationTests` (tablas presentes).

### Falta (brecha objetiva del bloque 018)
- **No existen RPC ni grants de escritura** para responsables. `revoke all` + solo `grant select` → hoy **es imposible crear/editar/vincular responsables** desde cliente (ni frontend ni API), porque todas las funciones de escritura del proyecto son wrappers `security definer` RPC (patrón `rpc_matricular_alumno`, `rpc_crear_alumno...`), y de responsables no hay ninguno.
- Backend .NET: no hay `Permisos.Responsables`, controller, DTOs, ni servicio. `AlumnosController` es un stub (fuera de alcance real); `MatriculasController` es el patrón autoritativo (PG directo + `FijarClaimAsync` + `resolver_institucion_operacion` + filtro por `usuario_tiene_permiso_actual`).
- Frontend Angular: no existe página `/responsables`; el listado de alumnos no expone responsables.

### Legacy (documentado, NO se borra)
- **Namespace duplicado de permisos**:
  - `responsables.responsables.ver/crear/editar` (modulo `responsables`) — creado en **007**, asignado a operador. **LEGACY / sin uso**, no lo referencia ninguna función ni policy.
  - `academico.responsables.ver/crear/editar` (modulo `academico`) — creado en **009**, asignado a operador y **usado por el RLS real** (`usuario_puede_ver_responsable`, `usuario_puede_ver_alumno`) y coherente con el resto de `academico.alumnos.*`, `academico.matriculas.*`.
  - **Namespace vigente = `academico.responsables.*`** (decisión basada en que es el que usa la capa de seguridad real y el backend ya declara `academico.*`). El legacy `responsables.responsables.*` se conserva (documentar en cierre; no rotura de compatibilidad).
- `Alumno.cs` (modelo .NET no usado por la API real) expone `PadreId`/`TutorId` legacy.
- `PortalPadre` (frontend) consulta `alumnos.tutor_id` (columna inexistente en el esquema consolidado) y tablas `mensualidades`/`pagos` (no en este esquema). Es un prototipo; aislado, fuera de alcance salvo limpiezas seguras.

## Decisiones
1. **Crear migración 017** con RPCs de responsables (invoker base + wrapper `security definer`, patrón 008/009/011) + grants; NO reescribir 004/007/009.
2. **Namespace vigente: `academico.responsables.*`** — usado en RLC/RLS, backend (Permisos) y RPCs nuevos.
3. Backend: `ResponsablesController` replicando `MatriculasController` (transacción + claim + permiso por ámbito institucional).
4. Frontend: servicio `responsables.service.ts` consumiendo la API .NET (patrón `matriculas.service.ts`) + página `/responsables` + integración en detalle de alumno.
5. Sin DELETE físico. Desactivación soft con motivo para responsable y vínculo.
6. No se toca `portal-padre` (legacy) ni Supabase remoto.

## Pendientes derivados
- MIG017: `crear_responsable_*`, `editar_responsable`, `inactivar/reactivar_responsable`, `vincular_alumno_responsable`, `editar_vinculo`, `desactivar/reactivar_vinculo` (con invariante de principal en DB).
- Backend/frontend/tests/documentación (fases siguientes).
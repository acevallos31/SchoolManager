# Bloque 030 — Auditoría y matriz de contratos (checkpoint 030A)

Rama: `feature/arquitectura-api-030` (creada desde `main` tras merge de PR #52 = `8c3adc5`).
Fecha: 2026-09-07. Estado: **matriz solo documento, sin código** — base para 030B–030F.

## Alcance de la auditoría

Búsqueda exhaustiva en `frontend/schoolmanager-frontend/src` de `SUPABASE_CLIENT`,
`supabase.from(`, `supabase.rpc(` y servicios Angular que toquen Supabase directo
(excl. `*.spec.ts`).

### Hallazgo global

| Categoría | Archivos | Decisión |
|---|---|---|
| **Infraestructura/auth (permitida)** | `app/core/services/auth.ts` (sesión, `SUPABASE_CLIENT` para auth) | Se mantiene: es auth deliberada, documentada. |
| **Negocio — a migrar (deuda #10)** | `app/core/services/alumno.service.ts` · `ciclo-escolar.service.ts` · `estructura-academica.service.ts` · `configuracion.service.ts` | Objetivo de 030B–030E. |
| Falso positivo | `app/pages/responsables/responsables.ts` | `Array.from(...)` JS nativo, no Supabase. |
| Ya migrado | `app/core/services/matriculas.service.ts` | Usa `HttpClient` contra `MatriculasController` (bloque 022). |

**No existen** `AlumnosController`, `CiclosEscolaresController`,
`EstructuraAcademicaController` ni `ConfiguracionController` en el backend.
El patrón de controller es `ApiControllerBase` (conexión Npgsql como usuario
autenticado, `set_config('request.jwt.claim.sub')`, traducción de errores SQL).

---

## 1. Dominio ALUMNOS  →  030B

**Frontend:** `app/core/services/alumno.service.ts`
**Páginas:** listado / búsqueda / detalle de alumnos (y selector en matrículas).

| # | Operación frontend | Tabla/RPC actual | Lect./Esc. | Endpoint .NET existente | Endpoint objetivo faltante | Permisos | Tests |
|---|---|---|---|---|---|---|---|
| A1 | `listar()` | `supabase.from('alumnos')` join matrícula activa | L | — | `GET /api/alumnos` | `alumnos.ver` | Listado con matrícula activa; institución filtrada. |
| A2 | `buscarPaginado()` | `supabase.from('alumnos')` .range() + filtros búsqueda | L | — | `GET /api/alumnos?page&pageSize&busqueda&estado&institucionId` (PaginatedResult) | `alumnos.ver` | Paginación, búsqueda, filtro institución, permisos. |
| A3 | `obtenerPorId()` | `supabase.from('alumnos')` | L | — | `GET /api/alumnos/{id}` | `alumnos.ver` | 200/404; sin cruce institución. |
| A4 | `crear()` | `rpc_crear_alumno_nueva_persona_con_documento` | E | — | `POST /api/alumnos` (envuelve la RPC, **no reimplementa**) | `alumnos.crear` | Creación con documento; duplicado→409; institución. |
| A5 | `desactivar()` | `rpc_desactivar_alumno` | E | — | `POST /api/alumnos/{id}/desactivar` | `alumnos.desactivar` | Desactivación; 404; permisos. |
| A6 | `reactivar()` | `rpc_reactivar_alumno` | E | — | `POST /api/alumnos/{id}/reactivar` | `alumnos.desactivar` (o `editar`) | Reactivación; 404; permisos. |

Preserva: `codigo_interno`, institución, permisos y comportamiento actual.
Los RPC ya aplican RLS/aislamiento institucional vía `request.jwt.claim.sub`.

---

## 2. Dominio MATRÍCULAS  →  030C

**Frontend:** `app/core/services/matriculas.service.ts` — **ya usa API .NET** (bloque 022).

**Backend existente (reutilizable):** `MatriculasController` completo:
`GET /api/matriculas` (paginado+`cicloId/estado`/PaginatedResult), `GET /api/matriculas/{id}`,
`POST /api/matriculas` (envuelve `rpc_matricular_alumno`), `actualizar estado`
(`rpc_cambiar_estado_matricula`), `registrar`, `GET /api/matriculas/alumno/{id}`.

| # | Observación | Acción |
|---|---|---|
| M1 | Endpoints de matrículas **ya existen y reutilizan RPC**. | Sin implementación nueva salvo endpoint faltante detectado. |
| M2 | Reglas de transición (estados) **viven en RPC/DB** (`rpc_matricular_alumno`, `rpc_cambiar_estado_matricula`). | **No duplicar en C#.** |
| M3 | Las páginas de matrículas dependen de selectores que hoy usan `alumno.service`, `ciclo-escolar.service` y `estructura-academica.service`. | Se resuelven al migrar 030B/030D/030E. |

Tests existentes: `MatriculasControllerTests.cs`, `ConcurrenciaMatriculasTests.cs`.
Completar solo endpoints faltantes si el frontend reclama uno; verificar permisos,
institución y estados.

---

## 3. Dominio CICLOS / PERÍODOS  →  030D

**Frontend:** `app/core/services/ciclo-escolar.service.ts`
**Páginas:** configuración de ciclos escolares y períodos de matrícula
(selectores en matrículas y finanzas).

| # | Operación frontend | RPC actual | Lect./Esc. | Endpoint .NET existente | Endpoint objetivo faltante | Permisos | Tests |
|---|---|---|---|---|---|---|---|
| C1 | `listarCiclos()` | `rpc_listar_ciclos_escolares` | L | — | `GET /api/ciclos-escolares` | `ciclos.ver` | Listado; single/multi-institución; sin cruce. |
| C2 | `crearCiclo()` | `rpc_crear_ciclo_escolar` | E | — | `POST /api/ciclos-escolares` | `ciclos.crear` | Creación; fechas/estado validados por RPC→400; institución. |
| C3 | `actualizarCiclo()` | `rpc_actualizar_ciclo_escolar` | E | — | `PUT /api/ciclos-escolares/{id}` | `ciclos.editar` | Actualización; 404; institución. |
| C4 | `desactivarCiclo()` | `rpc_desactivar_ciclo_escolar` | E | — | `POST /api/ciclos-escolares/{id}/desactivar` | `ciclos.desactivar` | Desactivación; restricciones DB→400. |
| C5 | `reactivarCiclo()` | `rpc_reactivar_ciclo_escolar` | E | — | `POST /api/ciclos-escolares/{id}/reactivar` | `ciclos.desactivar`/`editar` | Reactivación. |
| C6 | `listarPeriodos()` | `rpc_listar_periodos_matricula` | L | — | `GET /api/ciclos-escolares/{id}/periodos` | `ciclos.ver` | Períodos del ciclo; institución. |
| C7 | `crearPeriodo()` | `rpc_crear_periodo_matricula` | E | — | `POST /api/ciclos-escolares/{id}/periodos` | `ciclos.editar` | Creación período; RPC valida→400. |
| C8 | `actualizarPeriodo()` | `rpc_actualizar_periodo_matricula` | E | — | `PUT /api/ciclos-escolares/{id}/periodos/{p}` | `ciclos.editar` | Actualización; 404; institución. |
| C9 | `desactivarPeriodo()` | `rpc_desactivar_periodo_matricula` | E | — | `POST /api/ciclos-escolares/{id}/periodos/{p}/desactivar` | `ciclos.desactivar` | Restricciones DB→400. |
| C10 | `reactivarPeriodo()` | `rpc_reactivar_periodo_matricula` | E | — | `POST /api/ciclos-escolares/{id}/periodos/{p}/reactivar` | `ciclos.desactivar`/`editar` | Reactivación. |

Frontera API .NET; **invariantes siguen en PostgreSQL/RPC** (no reimplementar en C#).
Pruebas single y multi-institución (aislamiento).

---

## 4. Dominio ESTRUCTURA ACADÉMICA  →  030E

**Frontend:** `app/core/services/estructura-academica.service.ts`
**Páginas:** configuración de grados, jornadas y secciones (selectores en matrículas).

| # | Operación frontend | RPC actual | Lect./Esc. | Endpoint .NET existente | Endpoint objetivo faltante | Permisos | Tests |
|---|---|---|---|---|---|---|---|
| E1 | `listarGrados()` | `rpc_listar_grados` | L | — | `GET /api/estructura-academica/grados` | `estructura.ver` | Listado; institución. |
| E2 | `crearGrado()` | `rpc_crear_grado` | E | — | `POST /api/estructura-academica/grados` | `estructura.editar` | Creación; duplicado→409. |
| E3 | `actualizarGrado()` | `rpc_actualizar_grado` | E | — | `PUT /api/estructura-academica/grados/{id}` | `estructura.editar` | Actualización; 404; institución. |
| E4 | `desactivarGrado()` | `rpc_desactivar_grado` | E | — | `POST /api/estructura-academica/grados/{id}/desactivar` | `estructura.editar` | Restricciones DB→400. |
| E5 | `reactivarGrado()` | `rpc_reactivar_grado` | E | — | `POST /api/estructura-academica/grados/{id}/reactivar` | `estructura.editar` | Reactivación. |
| E6 | `listarJornadas()` | `rpc_listar_jornadas` | L | — | `GET /api/estructura-academica/jornadas` | `estructura.ver` | Listado; institución. |
| E7 | `crearJornada()` | `rpc_crear_jornada` | E | — | `POST /api/estructura-academica/jornadas` | `estructura.editar` | Creación; duplicado→409. |
| E8 | `actualizarJornada()` | `rpc_actualizar_jornada` | E | — | `PUT /api/estructura-academica/jornadas/{id}` | `estructura.editar` | Actualización; 404; institución. |
| E9 | `desactivarJornada()` | `rpc_desactivar_jornada` | E | — | `POST /api/estructura-academica/jornadas/{id}/desactivar` | `estructura.editar` | Restricciones DB→400. |
| E10 | `reactivarJornada()` | `rpc_reactivar_jornada` | E | — | `POST /api/estructura-academica/jornadas/{id}/reactivar` | `estructura.editar` | Reactivación. |
| E11 | `listarSecciones()` | `rpc_listar_secciones` | L | — | `GET /api/estructura-academica/secciones?gradoId&jornadaId` | `estructura.ver` | Filtros; institución. |
| E12 | `crearSeccion()` | `rpc_crear_seccion` | E | — | `POST /api/estructura-academica/secciones` | `estructura.editar` | Creación; duplicado→409. |
| E13 | `actualizarSeccion()` | `rpc_actualizar_seccion` | E | — | `PUT /api/estructura-academica/secciones/{id}` | `estructura.editar` | Actualización; 404; institución. |
| E14 | `desactivarSeccion()` | `rpc_desactivar_seccion` | E | — | `POST /api/estructura-academica/secciones/{id}/desactivar` | `estructura.editar` | Restricciones DB→400. |
| E15 | `reactivarSeccion()` | `rpc_reactivar_seccion` | E | — | `POST /api/estructura-academica/secciones/{id}/reactivar` | `estructura.editar` | Reactivación. |

**Reutiliza RPC actuales y aislamiento institucional** (RLS/`request.jwt.claim.sub`).
Tests de no cruce entre instituciones (cada operación con institución A y B).

---

## 5. Dominio CONFIGURACIÓN (institución/contexto) — fuera del mínimo, se decide en 030F

**Frontend:** `app/core/services/configuracion.service.ts`
**RPC actuales:** `rpc_obtener_contexto_implementacion`, `rpc_actualizar_multiples_instituciones`,
`rpc_obtener_configuracion_institucion`, `rpc_crear_institucion`, `rpc_actualizar_institucion`.

| # | Operación frontend | RPC actual | Lect./Esc. | Endpoint .NET | Observación |
|---|---|---|---|---|---|
| CF1 | `obtenerContexto()` | `rpc_obtener_contexto_implementacion` | L | — | Contexto multi-institución. |
| CF2 | `actualizarMultiplesInstituciones()` | `rpc_actualizar_multiples_instituciones` | E | — | Config global. |
| CF3 | `obtenerConfiguracion()` | `rpc_obtener_configuracion_institucion` | L | — | Config por institución. |
| CF4 | `crearInstitucion()` | `rpc_crear_institucion` | E | — | Alta institución. |
| CF5 | `actualizarInstitucion()` | `rpc_actualizar_institucion` | E | — | Edición institución. |

**Nota de decisión:** este dominio no está en la lista mínima (Alumnos/Matrículas/
Ciclos/Estructura). Puede quedar como acceso deliberado documentado (030F) o migrarse
si el contrato lo permite. **Requiere decisión de permiso/nombre de endpoint.**

---

## Decisiones pendientes que podrían bloquear

1. **Permisos de aplicación no existen para alumnos/ciclos/estructura/config.**
   Solo hay `configuracion.conceptos_financieros.*` y `configuracion.planes_pago.*`.
   Para 030B–030E hay que decidir:
   - (a) reutilizar el esquema `alumnos.*`, `ciclos.*`, `estructura.*` y crearlos en RBAC, o
   - (b) una convención distinta. **Propuesta:** crear `alumnos.ver/crear/editar/desactivar`,
     `ciclos.ver/crear/editar/desactivar`, `estructura.ver/editar`.
   - Los RPC de DB ya exigen sus propios permisos RLS (tabla de permisos en DB);
     el permiso de *aplicación* es adicional (authorization policy de .NET).
2. **Dominio Configuración (institución/contexto):** fuera de los 4 mínimos; decidir en 030F.
3. **RPC `rpc_crear_alumno_nueva_persona_con_documento`** ya escribe (A4): el POST debe
   envolverlo llamando a la RPC, **sin reimplementar** la lógica de alta con documento.

## Estado

Solo documento de matriz (030A). Sin endpoints, sin cambios de frontend, sin commits de código.
Listo para revisión antes de 030B.

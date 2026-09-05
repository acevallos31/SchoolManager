# Regresión `codigo_interno` de Alumnos — root cause, flujo real y fix

Estado: ACEPTADO (implementado en `feature/ux-alumno-appshell-019`).
Tipo: bug fix + restauración de visibilidad/búsqueda. Sin cambio de esquema.

## Root cause

**No es una regresión de UI.** `codigo_interno` nunca fue seleccionado,
mostrado ni buscado en ningún read path de la historia del repo (comprobado
en `git log -S`/`git show` sobre `d24abca` Fase-1C, `35b95e0` PERF-01 y HEAD).
Es un **gap write-only**: el campo se captura y persiste, pero ningún camino
de lectura lo devuelve al frontend.

Evidencia (estado previo al fix):

- **Write path únicamente** — `CrearAlumnoInput.codigoInterno` se envía y el
  RPC de creación lo persiste con normalización
  `nullif(btrim(p_codigo_interno), '')` (migración
  `011_extender_creacion_alumno_con_documento.sql`). El formulario lo captura
  (`alumnos.html`) y la config puede marcarlo requerido
  (`codigo_interno_requerido`, migración `002`).
- **Sin consumidor de lectura** — los selects fijos de `listar()`,
  `buscarPaginado()` y `obtenerPorId()` en
  `core/services/alumno.service.ts` no incluían `codigo_interno`; el `.or()`
  de `buscarPaginado()` solo cubría `nombres/apellidos/rne`, y el filtro
  cliente `alumnosFiltrados` de `pages/alumnos/alumnos.ts` tampoco lo incluía.
- **Nunca renderizado** — la tabla de alumnos en `pages/alumnos/alumnos.html`
  no tenía columna de código interno.

La columna sí existe en DB con constraint de unicidad
`ux_alumnos_codigo_interno_por_institucion` (único por institución) →
integridad de datos correcta y apta para mostrarse.

## Severidad y pérdida de datos

**No hubo pérdida de datos.** La columna se persiste correctamente al crear;
lo perdido es *visibilidad y accesibilidad*: un centro que exige el código
interno no podía confirmar el valor ya guardado, ni encontrarlo por búsqueda.
La restauración es exclusivamente de los read paths y de la UI.

## Flujo real verificado (user flow)

1. **Listado** — `pages/alumnos/alumnos.ts` llama a `listar()` (eager) y
   renderiza la tabla.
2. **Búsqueda en el listado** — `alumnosFiltrados` filtra en cliente por
   nombre/identidad/RNE/ciclo/grado/sección; ahora también por código interno.
3. **Crear alumno** — formulario captura campos obligatorios según config,
   incl. código interno si está configurado como requerido; se envía por
   `CrearAlumnoInput` → RPC de creación (persiste con normalización).
4. **Persistencia/lectura posterior** — tras el fix, el valor persiste y el
   read path (`listar`/`buscarPaginado`/`obtenerPorId`) lo devuelve y se
   muestra en la tabla.

## Fix aplicado (opción a, sin localStorage)

- `AlumnoListado` (+`codigoInterno: string | null`) y tipos `AlumnoRow` /
  `AlumnoBasicoRow` (+`codigo_interno`).
- Se añade `codigo_interno` al select y al mapeo de `listar()`,
  `buscarPaginado()` y `obtenerPorId()`.
- `buscarPaginado()`: `.or()` ampliado con `codigo_interno.ilike.%<termino>%`.
- Filtro cliente `alumnosFiltrados` incluye `alumno.codigoInterno`.
- Tabla: nueva columna "Código interno" (`—` si vacío), header y `colspan`
  ajustados; placeholder del buscador actualizado.
- Sin uso de `localStorage`.

## Pruebas de regresión añadidas

`alumno.service.spec.ts`:
- `codigoInterno` viaja en `listar()`, `obtenerPorId()` y `buscarPaginado()`
  (select contiene `codigo_interno`, valor mapeado).
- búsqueda por término incluye `codigo_interno.ilike` en el `.or()`.
- sin término no aplica `.or()` (comportamiento previo intacto).

`pages/alumnos/alumnos.spec.ts`: fixtures actualizados con `codigoInterno`.

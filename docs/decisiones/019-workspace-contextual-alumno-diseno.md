# Diseño — Workspace contextual del alumno (AppShell)

Estado: IMPLEMENTADO (F2/F3/F4, rama `feature/ux-alumno-workspace-cierre-019`;
ver `019-ux-cierre.md` para el cierre y la deuda pendiente).
Objetivo 4 de la sesión 019-ux. Alcance: AppShell compartido + workspace
contextual master-detail del alumno + acciones contextuales en la página de
alumnos.

## Contexto / problema

Hoy la experiencia del alumno se fragmenta en varias páginas sin continuidad
contextual: el listado de alumnos (`pages/alumnos`), el flujo de matrícula
(`pages/matriculas`) y la gestión de responsables (`pages/responsables`) son
pantallas separadas. Desde la fila de un alumno se puede saltar a matrícula o
responsables, pero cada destino es una página completa sin mantener el foco en
*ese* alumno. No existe una vista de ficha/detalle que reúna la información de
un alumno concreto (verificable: `pages/alumnos/` no contiene componente de
ficha).

El alumno en `AlumnoListado` es ya un agregado natural: persona + identidad +
RNE + código interno + estado + matrícula actual (ciclo/grado/sección).

## Propuesta

Un **workspace contextual** centrado en una entidad (`alumno`) dentro de un
`AppShell` compartido:

1. **AppShell (shell común)** — marco de navegación (barra lateral/top) con
   las secciones existentes. El *contexto* activo (alumno seleccionado) vive a
   nivel de shell y lo comparten los paneles inferiores.
2. **Vista lista → detalle (master-detail)** — el listado de alumnos es el
   "master"; al seleccionar una fila se abre un panel de detalle "detail" que
   muestra los datos del alumno (nombre, identidad, RNE, código interno,
   estado) y sus *contextos*: matrícula actual e histórico, responsables.
3. **Acciones en contexto** — "Matricular", "Responsables", "Editar",
   "Desactivar/Reactivar" operan sobre el alumno seleccionado y permanecen en
   el mismo workspace en vez de disparar una navegación a una página aislada.

## Guía de UX (referencia local, no vertic)

La comparación visual con vertic-platform/vertic-demo queda **bloqueada**
(ver `019-ux-referencia-vertic-bloqueado.md`); el diseño se apoya en los
patrones ya presentes en el repo (matriculas, responsables) como referencia
local de consistencia.

## Fases

- **F1 (hecho):** restaurar visibilidad/búsqueda de código interno en el
  listado (ver `019-regresion-codigo-interno-alumno.md`).
- **F2 (hecho):** `AppShell` compartido en `src/app/layout/app-shell/`; la ruta
  `/alumnos` queda como hijo del shell.
- **F3 (hecho):** panel de detalle contextual del alumno (master-detail) en
  `pages/alumnos` reutilizando el agregado de `AlumnoListado`, con selector
  `.btn-detalle-fila` (aria-expanded), panel `.alumno-detalle` y estado de
  selección en el componente (sin localStorage).
- **F4 (hecho):** acciones contextuales Matricular y Responsables en el
  workspace, operando sobre el alumno seleccionado. "Editar" queda como deuda
  (no existe ruta/servicio de edición).

## Deuda documentada (ver `019-ux-cierre.md`)

- Editar alumno (F4 parcial): no hay ruta/servicio de edición.
- Adopción global del AppShell en el resto de páginas (hoy solo cubre
  `/alumnos`).
- Referencia visual Vertic (bloqueo externo).

## Fuera de alcance

- No se implementa aquí el rediseño de AppShell.
- No toca backend ni esquema.
- Sin cambios en `pages/matriculas` ni `pages/responsables`.

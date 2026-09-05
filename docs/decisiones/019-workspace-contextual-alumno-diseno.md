# Diseño — Workspace contextual del alumno (AppShell)

Estado: PROPUESTA (solo diseño; **no** implementado).
Objetivo 4 de la sesión 019-ux. Alcance deliberadamente acotado: documento de
diseño, sin rediseño grande de AppShell.

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

## Fases sugeridas (implementación futura)

- **F1 (ahora, ya hecho como fix acotado):** restaurar visibilidad/búsqueda de
  código interno en el listado (ver `019-regresion-codigo-interno-alumno.md`).
- **F2:** introducir el `AppShell` compartido (sin cambios de comportamiento de
  las páginas actuales).
- **F3:** panel de detalle contextual del alumno (master-detail) reutilizando
  el agregado de `AlumnoListado`.
- **F4:** mover acciones contextuales (matrícula/responsables) al workspace.

## Fuera de alcance

- No se implementa aquí el rediseño de AppShell.
- No toca backend ni esquema.
- Sin cambios en `pages/matriculas` ni `pages/responsables`.

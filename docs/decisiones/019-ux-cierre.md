# Cierre — Fase 019-ux: Workspace contextual del alumno

Estado: COMPLETADO (F2/F3/F4). Rama `feature/ux-alumno-workspace-cierre-019`
basada en `main` (`70d489c`). PR contra main **sin mergear**.

## Alcance entregado

- **F2 — AppShell compartido:** componente `src/app/layout/app-shell/`
  (`app-shell.ts/html/css/spec.ts`). La ruta `/alumnos` queda como hijo del
  shell (`app.routes.ts`).
- **F3 — Detalle contextual master-detail:** en `pages/alumnos`, seleccionar
  una fila (botón `.btn-detalle-fila`, con `aria-expanded`) abre el panel
  `.alumno-detalle` reutilizando el agregado de `AlumnoListado` (nombre,
  identidad, RNE, código interno, estado, matrícula actual). El estado de
  selección vive en el componente (`alumnoSeleccionadoId` / getter
  `alumnoSeleccionado`); **no** usa localStorage.
- **F4 — Acciones contextuales:** Matricular y Responsables operan sobre el
  alumno seleccionado dentro del workspace (navegan con `alumnoId`).
  Desactivar/Reactivar permanecen en la fila. "Editar" no se implementa (ver
  deuda).
- **CSS responsive:** workspace en una columna, dos columnas con panel sticky
  en ≥1000px, breakpoint tablet (900px) y móvil (640px), tabla con
  `overflow-x: auto`.

## Validación

- Suites de test: **131 passed / 131** (`npx ng test --watch=false`).
  Incluye 4 tests nuevos del panel contextual y los 5 del AppShell.
- Build de producción: `npx ng build --configuration production` → sin errores.
- `git diff --check` → limpio.
- Commit(s) pequeños en español (Conventional Commits), push, PR contra `main`
  sin mergear.

## Causa raíz del fallo de los tests contextuales

Los 4 tests nuevos del panel contextual fallaban aunque componente y template
eran correctos. La causa raíz era **cómo se activaba la selección en el test**,
no la implementación:

- El primer render asíncrono (`ngOnInit` → `cargarAlumnos()`) sí se reflejaba en
  el DOM, pero **las mutaciones síncronas posteriores al render inicial no
  re-renderizaban el panel** aunque se llamara `fixture.detectChanges()` (ni con
  doble `detectChanges()` ni con `whenStable()`): `@if (alumnoSeleccionado)`
  quedaba como `false` y el `<aside class="alumno-detalle">` nunca se emitía,
  pese a que el getter devolvía un objeto y `alumnoSeleccionadoId` estaba fijado
  en la misma instancia del componente.
- En cambio, **la interacción DOM real (click sobre `.btn-detalle-fila`, evento
  envuelto por NgZone) sí re-renderizaba el panel** y los tests pasaban. Esto se
  verificó de forma reproducible en specs de aislamiento desechables.
- **Resolución (correcta, sin tocar producción):** se reescribieron los 4 tests
  para disparar la selección con **click real sobre `.btn-detalle-fila`** +
  `await fixture.whenStable()` + `fixture.detectChanges()`, verificando
  `aria-expanded`, `.alumno-detalle`, contenido del alumno y cambio de
  selección. No se modificó componente, template ni arquitectura para "arreglar"
  un test mal sincronizado.

Dos de esos tests además tenían un defecto propio: recreaban `fixture` con
`destroy()` **sin reasignar la variable `component`**, dejando una instancia
stale; se corrigió al reasignar `component = fixture.componentInstance`.

## Deuda pendiente

- **Editar alumno (F4 parcial):** no se implementó la acción contextual
  "Editar" porque no existe ruta ni servicio de edición de un alumno.
- **Adopción global del AppShell:** hoy solo `/alumnos` usa el shell; el resto
  de páginas (matriculas, responsables, dashboard) aún no lo adoptan.
- **Referencia Vertic:** objetivo 3 bloqueado por ausencia de repos/URL de
  `vertic-demo` (ver `019-ux-referencia-vertic-bloqueado.md`).

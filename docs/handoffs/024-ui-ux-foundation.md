# Bloque 024 — UI/UX Foundation + AppShell Global (Fase 0: auditoría visual)

## Estado: LISTO PARA REVISIÓN — PR contra main (sin mergear por regla)

Rama: `feature/ui-ux-foundation-024`.
Base: `origin/main` = `702d2f1` (merge PR #46 de 023).
PR contra main: **abierto — NO mergear** (espera revisión humana).
Solo frontend. No DB. No migraciones. No backend. No 025. No Figma. No producción/Supabase remoto.

## Objetivo cumplido
Construir la base visual profesional, moderna, responsive y escalable de
SchoolManager: design tokens como fuente única de verdad, tipografía/jerarquía,
AppShell global reusable para todas las rutas admin, componentes visuales base y
primera adopción en AppShell + Dashboard + `/alumnos` (piloto master/detalle).
Dirección inspirada en Vertic (calidad), **sin copiar** su HTML/CSS.

## Qué se hizo

### Fase 0 — Auditoría visual (hallazgos documentados)
- `styles.css` tenía tokens `--sm-*` básicos + hardcodes (`#153554`, `#d7a86d`)
  repetidos en dashboard; AppShell solo envolvía `/alumnos`.
- `/dashboard` **duplicaba el sidebar completo**; el resto de rutas
  (`/matriculas`, `/responsables`, `/cargos`, `/pagos`, `/configuracion` +
  subvistas) eran top-level **sin shell** con botón "Volver" a `/dashboard`.
- `UsuarioActual` expone solo `{id, personaId, roles[], permisos[]}`.
- Sin carpeta de componentes compartidos; CSS plano (sin SCSS), sin dependencias UI.

**Decisión estructural (presentada y aprobada por el usuario): Opción A** —
AppShell padre de **todas** las rutas admin; `/login` y `/portal-padre` quedan fuera.

### 1. Design tokens + primitivas (`src/styles.css`)
Tokens `--sm-*` completos: paleta + estados (success/warning/error/info/soft),
superficies, texto, bordes, spacing (escala 4px `--sm-space-1..7`), radius,
sombras, tipografía (tamaños/pesos/line-height), alturas de control, sidebar/topbar,
breakpoints y z-index. Primitivas globales: `.sm-page-title/.sm-section-title/
.sm-eyebrow/.sm-body/.sm-helper/.sm-label/.sm-metadata`, `.sm-btn` (+`--primary/
--secondary/--ghost/--danger/--sm/--block`), `.sm-link-action`, `.sm-card`
(+`__header/__body`), `.sm-badge` (+`--success/warning/error/info/neutral`),
`.sm-input/.sm-select/.sm-textarea/.sm-field`, `.sm-table-wrap/.sm-table`,
`.sm-state`, `.sm-spinner`, `.sm-alert` (+`--success/--error`).

### 2. AppShell global (`layout/app-shell/`)
Topbar fija, sidebar persistente en escritorio, **drawer móvil con overlay**,
navegación activa por ruta (`esRutaActiva`), filtrado por permisos
(`mostrarItem`), área de identidad con **roles reales** (sin inventar
nombre/institución), logout a `/login`.

### 3. Rutas (`app.routes.ts`)
Todas las rutas admin anidadas como hijas del AppShell; guards y
PermissionGuard intactos (redirige a `/dashboard`).

### 4. Dashboard
Sin sidebar/topbar/logout duplicados (el chrome lo da el shell); solo contenido
con la foundation (hero + métricas + `puedeVerCargos`).

### 5. `/alumnos` (piloto)
Reescrito sobre primitivas (`sm-btn`, `sm-table`, `sm-badge`, `sm-card`) sin
rediseñar la lógica master/detalle. **Conserva las clases que los 33 tests
existentes consultan** (`.btn-matricular-fila`, `.btn-detalle-fila`,
`.alumno-detalle`, `.btn-cerrar-detalle`, `.page-container`, `.page-header`).
CSS depurado: eliminados estilos que duplicaban primitivas; queda maquetación
específica master/detalle.

### 6. Tests
Spec AppShell ampliado a **9 tests** (navegación, drawer móvil, filtrado por
permisos, config por permiso) — sin verificar píxeles/CSS exacto. Los 33 tests
de alumnos intactos.

## Commits (rama, oldest→newest)
1. `63553ac` feat(ui): design tokens y primitivas base globales
2. `b347393` feat(ui): AppShell global con topbar, sidebar y drawer responsive
3. `9d063e7` refactor(ui): rutas admin anidadas bajo AppShell y dashboard sin sidebar duplicado
4. `8e0ccc2` feat(ui): aplica foundation a /alumnos como pantalla piloto master-detail
5. `0214003` test(ui): navegación, drawer móvil y filtrado por permisos del AppShell
6. `PENDIENTE` docs: design-system + handoff 024 + AI_CONTEXT

## Qué NO se hizo (y por qué)
- **Rediseñar** matrículas, responsables, cargos, pagos, configuración ni portal
  responsable: quedaron envueltos por el shell con su contenido interno intacto
  (per §6 primera adopción).
- **Migrar deuda #10** (Supabase directo en páginas de negocio): prohibido en 024;
  bloque dedicado post-UX.
- **Tocar** lógica financiera/académica, contratos API, servicios ni behavior.
- **Introducir** dependencias UI, SCSS ni fuente externa (rule §8 y t2).

## Documentación nueva/actualizada
- `docs/ui/design-system.md` (nuevo): tokens, componentes, reglas de uso,
  estados, responsive, decisiones inspiradas en Vertic sin copiarlo.
- `docs/handoffs/024-ui-ux-foundation.md` (este archivo).
- `docs/AI_CONTEXT.md`: actualizado con el estado real de 024 (AppShell global,
  foundation aplicada a Dashboard/Alumnos, resto por migrar).

## Validación ejecutada
- FE build development: OK (post AppShell, post rutas+dashboard, post alumnos).
- FE hit `app-shell.spec.ts`: 9/9 OK.
- Suite FE completa, production build, coverage gate y `git diff --check`:
  ejecutados en la validación final antes del push (resultado en el cuerpo del PR).

## Restricciones respetadas
Sin producción/Supabase remoto. Sin backend/DB/migraciones. Sin 025/Figma.
Sin rediseño total de módulos. Commits pequeños por capas. Push a la rama.
PR contra main **sin mergear**.

## Para la revisión / próximos bloques
- La foundation és la referencia para migrar el resto de pantallas sin
  rediseñarlas desde cero: consumir tokens/primitivas y eliminar chrome duplicado.
- `/alumnos` demuestra el patrón master/detalle con la foundation.
- No mergear sin revisión humana (`git checkout main && git pull && git merge
  feature/ui-ux-foundation-024` solo tras aprobación).
# Handoff — Bloque 028: Cierre UX Final

## Objetivo
Cierre final de la adopción de la foundation UI `sm-*` (Bloques 024–027):
auditoría UX global, correcciones pequeñas (responsive + accesibilidad básica),
limpieza de CSS paralelo/huérfano, validación (tests + build), revisión visual y
documentación de cierre sobre la app admin completa.

## Rama y commits
- Rama: `feature/ui-ux-cierre-028` (base `origin/main`, incluye 024–027).
- Commits (conventional, español):
  1. `docs(agent): agrega prompt operativo del bloque 028`
  2. `fix(ui): jerarquía de z-index del drawer y a11y del menú móvil (Escape/aria)`
  3. `fix(a11y): modales de configuración con role=dialog y labels de cuotas asociadas`
  4. `refactor(css): elimina CSS paralelo/huérfano y centraliza ancho de contenedor`
  5. `docs(handoff): agrega handoff del bloque 028`

## Alcance
Solo frontend Angular. Sin cambios de lógica de negocio ni de `.ts`
(excepto ninguno); sin backend/DB/RPC/contratos/portal-responsable.

## Auditoría global
4 auditores read-only (un grupo por zona: app-shell, login, dashboard,
configuración) contra la foundation `sm-*`, con reporte estructurado
(severidad/archivo:línea/corrección). Resultado: la app está **en buena
coherencia** tras 024–027; los hallazgos reales se concentraron en
app-shell y modales de configuración; el resto fueron CSS duplicado/huérfano
y valores literales repetidos. Login se declara excepción consciente (landing
bespoke hero/gradiente/glass, deliberadamente ajena a la foundation admin;
migrarla exigiría rediseño + sign-off visual, no se fuerza media-migración).

## Correcciones aplicadas

### app-shell (drawer) — bug real de z-index + a11y
- **z-index (severidad alta)**: `.sm-shell__overlay` usaba el token overlay
  (`--sm-z-overlay:400`) → en móvil el scrim quedaba **por encima** del drawer
  (300), tapando la sidebar. Ahora `z-index: calc(var(--sm-z-drawer) - 1)`.
  Jerarquía resultante: scrim **299** < drawer **300** < modal **400+**.
- **A11y del drawer** (`app-shell.html`):
  - toggle con `[attr.aria-expanded]="navAbierta"` y `aria-label` dinámico
    («Abrir/Cerrar menú de navegación»).
  - cierre con **Escape**: `(keydown.escape)="navAbierta && cerrarNav()"`.
  - `<aside>` del drawer con `aria-label="Secciones"`.
- **A11y móvil sin regresión visual** (`app-shell.css`, ≤1100px): la sidebar
  cerrada sale del tab-order/árbol a11y vía `visibility` con transición
  retardada que **preserva el slide-out** (`transform translateX(-100%)`);
  visible al abrir. Alternativa `aria-hidden` dejaba foco en enlaces ocultos o
  rompía la transición.

### Modales de configuración — semántica de diálogo
Los modales de matrículas/pagos ya tenían `role="dialog"`. Faltaba en los de
configuración; se añadió `role="dialog" aria-modal="true"` con `aria-label`
condicional (Editar/Nuevo) a los **6 forms-modal**:
- `configuracion-ciclos.html`: ciclo y período.
- `configuracion-estructura-academica.html`: grado, jornada y sección.
- `configuracion-conceptos-financieros.html`: concepto.
Planes de pago no usa modal superpuesto (editor inline `.plan-editor`), por lo
que no requiere `role="dialog"`.

### Labels de cuotas (planes de pago)
Las 5 labels por fila de cuota (`Orden`, `Concepto`, `Descripción`, `Monto`,
`Vencimiento`) ahora tienen `for`/`id` indexados por fila (`co{{$index}}`, …).

### Limpieza de CSS
- **Eliminado CSS paralelo redundante**: `.sm-field` (byte-idéntico a la
  foundation `styles.css`) en `configuracion-planes-pago.css`.
- **Eliminado CSS muerto**: bloque `.card` sin uso en `configuracion.css`.
- **Token `--sm-container: 1180px`** añadido a la foundation y sustituido en
  las 5 páginas de tablas densas (matriculas/cargos/pagos/alumnos/
  responsables). Mismo valor → cero cambio visual; elimina el literal repetido.
- Anchos de 1100/960/1080px en otras páginas se dejan como valores locales
  intencionales por densidad de módulo.

## Tests
`npx ng test --watch=false`: **184/184 passed** (25 archivos). Coverage líneas
**68.13%** (baseline 68.11%).

## Build
`npx ng build` OK. Bundle principal `main` 720.60 kB raw / 162.07 kB transfer;
`styles` 9.56 kB. Sin errores de compilación de plantillas.

## Revisión visual
Revisión **estática de layout/CSS** (mismo método y limitación que 024–027, por
ausencia de staging/preview con credenciales y datos — regla AGENTS.md §5, no
inventar entornos). Los cambios de este bloque son casi todos aditivos de
accesibilidad (`role`, `aria-*`) o **valor-preservantes** (token `--sm-container`
idéntico; `.sm-field` redundante cuya copia global sigue aplicando; `.card` nunca
usado), por lo que el riesgo de regresión visual es mínimo y queda cubierto por
tests + build.

Verificación de coherencia sobre los cambios reales:
- Tras eliminar `.sm-field` de página, la foundation **global** `styles.css`
  (no encapsulada por componente) sigue aplicando el estilo a los inputs de
  los formularios → sin cambio visual.
- `--sm-container` = 1180px (mismo valor literal) en 5 páginas → layout
  idéntico.
- Fix del drawer es solo móvil (scrim 299 < drawer 300); en desktop el drawer
  fijo no se ve afectado.

## Restricciones respetadas
- Conventional Commits en español; commits pequeños por tema.
- Sin estilos paralelos duplicando la foundation (se eliminaron).
- Sin cambios de lógica de negocio ni de `.ts`.
- Sin backend/DB/migraciones/RPC/contratos/portal-responsable.
- **No se mergeó**: el PR contra `main` queda para revisión humana.

## Pendientes reales (riesgos residuales)
- Revisión **E2E autenticada** (login → guardas → datos vivos) cuando exista
  entorno con credenciales/datos reales; la revisión estática no la sustituye
  (misma limitación documentada en 024–027).
- Login sigue como excepción consciente fuera de la foundation (ver Alcance).
- Deuda #10 (páginas de negocio que consultan Supabase directo) para bloque
  dedicado post-UX.

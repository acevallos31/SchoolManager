# Handoff — Bloque 026: Adopción de foundation UI en Cargos y Pagos (finanzas)

## Objetivo
Migrar visualmente a la foundation de UI (Bloque 024/025) el bloque financiero
que aún conservaba su contenido interno sin rediseñar: `/cargos` y `/pagos`,
**sin cambiar lógica de negocio** y **sin crear estilos paralelos**.

## Alcance
- Solo frontend Angular (HTML + CSS). No se modificó ningún `.ts` de negocio:
  la lógica de cargos (estados, saldo, resumen) y pagos (registro, aplicación
  por cargo, anulación, detalle) quedó intacta.
- No se tocó backend/DB/migraciones/RPC/contratos API/portal-responsable.
- Deuda #10 (páginas que consultan Supabase directo) sigue pendiente, sin tocar.

## Rama y commits
Base: `main` = `38fe128` (merge PR #48, Bloque 025). Rama: `feature/ui-ux-finanzas-026`.

Cadena de commits (sobre `38fe128`):
1. `9956667` — `feat(ui): adopta foundation sm-* en página de cargos (bloque 026)`
2. `469e7fd` — `feat(ui): adopta foundation sm-* en página de pagos y resuelve warning de budget (bloque 026)`
3. *(final, pendiente al cierre)* — `docs(ui): cierra bloque 026`

## Módulos migrados
| Módulo | Archivos | Cambios |
|---|---|---|
| Cargos | `cargos.html/.css` | HTML → primitivas `sm-*`; CSS de página reducido a maquetación local (grids resumen, `.col-acciones`, `.badge-inline`) |
| Pagos | `pagos.html/.css` | HTML → primitivas `sm-*` (incluye form de registro, modal de detalle); CSS de página reducido a maquetación local |

Lógica funcional intacta en ambos; los `.ts` no cambiaron.

## Detalles de adopción
- **Cargos**: títulos `sm-page-title`; alerta `sm-alert--error/success`;
  tarjetas KPI `sm-card` con `sm-eyebrow`; tabla `sm-table`; badges de estado
  `sm-badge--warning` (pendiente/parcial), `--success` (pagado), `--neutral`
  (anulado); "Vencido" como `sm-badge--error`; botón "Cobrar" `sm-btn--primary
  sm-btn--sm`; empty/loading via `sm-state` + `sm-spinner`.
- **Pagos**: resumen KPI (cobrables / saldo pendiente / pagos registrados);
  form de registro en `sm-card` con inputs `sm-input`, tabla de montos
  `sm-table` e inputs de monto `input-monto sm-input`; "Confirmar pago" con
  doble-submit prevenido por `[disabled]` (lógica intacta); tabla de pagos con
  badges `sm-badge--success` (registrado) / `--neutral` (anulado); motivo de
  anulación en `sm-card`; detalle en modal `sm-modal-overlay` + `sm-modal`.
- **Fondos monetarios legibles**: valores en mono? No — valores de resumen con
  `--sm-fs-xl` y peso bold; los `#Recibo` en `--sm-font-mono`.

## Extensiones de foundation (en `src/styles.css`)
**Ninguna.** Todos los primitivos usados (`sm-btn`, `sm-card`, `sm-badge`,
`sm-table`, `sm-input`, `sm-alert`, `sm-state`, `sm-spinner`, `sm-modal*`,
`sm-eyebrow`, `sm-section-title`, `sm-page-title`, `sm-label`, `sm-helper`)
ya existían en la foundation. Se respetó la regla: **no extraer primitiva sin
uso real/inmediato**.

Los KPIs de resumen (`resumen-grid`/`resumen-card`/`resumen-valor`) quedan como
**maquetación local por página** (mismo criterio que `.sm-stats` del Dashboard,
que tampoco es global). No se promovió a `styles.css` para no ampliar el
archivo compartido ni el alcance del bloque.

## Warning de budget (`pagos.css`)
**Resuelto.** `pagos.css` pasó de **4720 B → 2596 B** al eliminar la
duplicación de foundation (botones/tabla/badges/modal/inputs vivían repetidos
en el CSS de página). Queda bajo el límite de 4 kB de `componentStyle` del
`angular.json`. Confirmado: la build de producción ya **no emite** el warning
de budget. No se modificó ningún budget en la configuración.

## Tests
- Cargos: 8/8 · Pagos: 9/9 (lógica-only, sin hooks DOM de layout).
- Suite FE completa: **184/184** en 25 archivos (misma cuenta que 025; no se
  añadieron specs porque la migración fue puramente visual — no hubo cambio de
  lógica que probar).

## Validación
- Suite FE completa: 25 archivos / 184 tests, exit 0.
- Production build: OK, sin warnings de budget.
- Coverage (v8): Lines 68.56% / Statements 60.51% (sin regresión; la migración
  no añadió lógica TS).
- `git diff --check`: sin errores.
- Responsive: CSS local con breakpoint `700px` para `.page-header` y
  `form-pie`/`form-botones` en pagos (coincide con breakpoints usados en 025).
  Falta la revisión visual desktop/tablet/mobile en navegador (pendiente).

## Restricciones respetadas
- Conventional Commits en español; commits pequeños por módulo.
- Sin estilos paralelos: todos los primitivos vienen de `styles.css`.
- Sin cambios de lógica de negocio ni de `.ts` en ningún módulo.
- Sin backend/DB/migraciones/RPC/contratos API/portal-responsable.
- No se mergeó: el PR contra main queda para revisión humana.

## Pendientes reales
- Revisión visual en navegador (desktop/tablet/mobile) del bloque financiero
  adoptado, antes de considerar el PR 026 definitivo.
- Deuda #10 (páginas de negocio que consultan Supabase directo) sigue pendiente
  para bloque dedicado post-UX.
- Resto del ecosistema de UI/UX (si queda alguna página sin adoptar) para un
  bloque posterior.

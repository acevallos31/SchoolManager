# Handoff — Bloque 027: UI/UX portal del responsable (adopción foundation sm-*)

**Estado:** CERRADO (código + docs) — PR final abierto contra `main`, **sin mergear por el agente**.
**Rama:** `feature/ui-ux-portal-responsable-027`
**Base:** `main` = `5acab12` (incluye merge del PR #49 / bloque 026)
**Fecha:** 2026-09-06

## Alcance
Migración 100% de presentación del portal del responsable
(`frontend/schoolmanager-frontend/src/app/pages/portal-padre/`) a la foundation
`sm-*` de `styles.css`. **Sin cambios en backend/DB, sin cambio de lógica
funcional, sin tocar contratos ni el servicio** `core/services/portal-responsable.service.ts`
(solo lectura). Portal **standalone**, fuera del AppShell admin (su cabecera es
maquetación local, mismo criterio que `/login`).

## Commits
- `86f2552` — `feat(ui): adopta foundation sm-* en portal responsable (bloque 027)`
  Migración visual de `.html`, `.css` y helpers de presentación en `.ts`.
- *(fix)* — `fix(ui): elimina clase huérfana sm-tab--resumen en portal responsable`
  La primera pestaña llevaba una clase inexistente en la foundation (las otras
  dos tabs no usan clase base); se alinea al patrón correcto (`sm-tab--active`
  condicional). Post-verificación sobre `86f2552`.
- *(docs)* — `docs(ui): documenta cierre del bloque 027 y sanea AI_CONTEXT/design-system`

## Cambios por archivo
### `portal-padre.html` (reescrito, 278 líneas)
Consume solo primitivas `sm-*`: cabecera portal propia (marca `sm-eyebrow` +
título + `sm-btn--secondary` Cerrar sesión), selector de hijos como botones
`sm-btn--sm` con `aria-pressed`, resumen KPI en `sm-card`/`sm-eyebrow`,
pestañas `sm-tabs`, tablas `sm-table`/`sm-table-wrap`, badges `claseBadge`,
estados `sm-state`/`sm-spinner`/`sm-alert--error`, expansión de aplicaciones
con `sm-btn--ghost`. No introduce los textos `'tarjeta'` ni `'Pagar'`
(constraints de la spec del portal: `not.toContain(...)`).

### `portal-padre.css` (reducido, 221 líneas / 4.446 B crudos)
Solo maquetación específica del portal y su cabecera (fuera del AppShell) +
tokens `--sm-*`. Sin primitivos paralelos de la foundation (botones, cards,
badges, tablas, tabs, alertas se consumen de `styles.css`).

### `portal-padre.ts` (+28 líneas de helpers de presentación únicamente)
- getter `estadoLabel: Record<string,string>` — etiquetas legibles
  (pendiente/parcial/pagado/registrado/anulado/activa).
- `labelEstado(estado)` — lookup con fallback al valor crudo.
- `claseBadge(estado)` — estados crudos del contrato → variantes
  `sm-badge--*` (warning/success/neutral/info).
Sin cambio de lógica de negocio ni de métodos existentes.

## Validación
- Spec del portal: **14/14 PASS**
- Suite FE completa: **184/184 PASS** (25 archivos, Vitest + coverage v8)
- Coverage (suite completa): Lines **68.25%**, Statements 59.98% (baseline 026
  ~68.32%; sin regresión relevante).
- Build de producción: **exit 0**, salida cruda **sin ninguna línea** de
  `budget|exceed|maximum|warning|anyComponentStyle` → **NO hay warning de
  budget** (el CSS del portal queda bajo el límite `componentStyle`, validado
  sobre el CSS minificado).
- `git diff --check`: limpio.
- Revisión visual estática (mismo criterio aprobado que 026): arneses que
  renderizan el DOM real de la plantilla + CSS real + foundation, con datos
  representativos, en `/root/block027_review/` (`cargos-data.html`,
  `pagos-data.html`, `estados.html`, `capture.sh`, `shots/` con 9 PNG:
  cargos/pagos/estados × desktop 1280 / tablet 820 / móvil 390). Sin
  regresiones: sin overflow de página, tablas con scroll contenido, detalle
  expandido bien anidado, badges semánticos, KPIs legibles, estados
  loading/empty/error claramente diferenciados.

## Riesgo residual (declarado)
Revisión visual **estática, no E2E autenticado** (igual que 026/024): el portal
requiere Supabase Auth (nube) + backend .NET con datos vivos para ejercitarse
en navegador; no hay staging/preview ni credenciales de login en el entorno →
la validación visual quedó en modalidad estática de DOM + CSS. No se
inventaron credenciales ni entornos.

## Docs saneados
- `docs/AI_CONTEXT.md`: 026 marcado como mergeado en `main` (`5acab12`, PR #49);
  027 en su rama en cierre.
- `docs/ui/design-system.md` §6: se actualiza el estado de adopción —
  cargos/pagos (026) y portal responsable `/portal-padre` (027) migrados.

## Para quien retome
- No rehacer la migración. El portal queda como página standalone migrada;
  siguiente adopción de foundation es independiente de este bloque.
- No tocar `portal-padre.service.ts` salvo nuevo requerimiento funcional.
- No mergear el PR del 027 por automatización; revisión humana antes.

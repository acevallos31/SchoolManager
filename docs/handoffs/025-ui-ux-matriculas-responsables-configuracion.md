# Handoff — Bloque 025: Adopción de foundation UI en Matrículas, Responsables y Configuración

## Objetivo
Migrar visualmente a la foundation de UI (Bloque 024) los módulos que aún
conservaban su contenido interno sin rediseñar: `/matriculas`, `/responsables`,
`/configuracion` (raíz) y los cuatro submódulos de configuración
(`ciclos`, `estructura-academica`, `conceptos-financieros`, `planes-pago`),
**sin cambiar lógica de negocio** y **sin crear estilos paralelos**.

## Alcance
- Solo frontend Angular (HTML + CSS + ajuste mínimo de `.ts` en matrículas:
  `claseEstado()` → clases `sm-badge--*`).
- No se tocó backend/DB/pagos/cargos/portal-responsable.
- Deuda #10 (páginas que consultan Supabase directo) sigue pendiente, sin tocar.

## Rama y commits
Base: `main` = `e876ae1` (merge PR #47, Bloque 024). Rama: `feature/ui-ux-adopcion-025`.

Cadena de commits (7, sobre `e876ae1`):
1. `ab012e1` — `feat(ui): extiende foundation con modal reusable` (bloque `.sm-modal` en `styles.css`)
2. `b913f8e` — `feat(ui): adopta foundation en matriculas`
3. `09ecf4c` — `feat(ui): adopta foundation en responsables`
4. `197c293` — `feat(ui): adopta foundation en configuracion` (raíz)
5. `4eb66e5` — `feat(ui): extiende foundation con tabs y acciones de modal` (`.sm-tabs`, `.sm-modal__actions` en `styles.css`)
6. `057a3db` — `feat(ui): adopta foundation en configuracion academia` (ciclos + estructura-academica)
7. `c9e82a1` — `feat(ui): adopta foundation en configuracion financiera` (conceptos-financieros + planes-pago)
8. *(final)* — `docs(ui): cierra bloque 025`

## Módulos migrados
| Módulo | Archivos | Estado |
|---|---|---|
| Matrículas | `matriculas.html/.css/.ts` | ✅ foundation, lógica intacta |
| Responsables | `responsables.html/.css` | ✅ foundation, lógica intacta |
| Configuración (raíz) | `configuracion.html/.css` | ✅ foundation, lógica intacta |
| Configuración / ciclos | `configuracion-ciclos.html/.css` | ✅ foundation, lógica intacta |
| Configuración / estructura-académica | `configuracion-estructura-academica.html/.css` | ✅ foundation (tabs), lógica intacta |
| Configuración / conceptos-financieros | `configuracion-conceptos-financieros.html/.css` | ✅ foundation (modal), lógica intacta |
| Configuración / planes-pago | `configuracion-planes-pago.html/.css` | ✅ foundation, editor de cuotas con primitivas |

## Extensiones de foundation (en `src/styles.css`)
Todas documentadas en `docs/ui/design-system.md` §4:
- `.sm-modal-overlay` / `.sm-modal` / `.sm-modal__title` / `.sm-modal__detail` — modal reusable (con reduced-motion).
- `.sm-modal__actions` — fila derecha de botones (Cancelar/Confirmar) del modal.
- `.sm-tabs` — pestañas de sección, extraído de `portal-padre.css` (024) a la foundation para no duplicar.

Regla respetada: extraer primitiva **solo cuando hay uso real/inmediato** (≥2 lugares o reutilización clara).

## Tests
Por módulo (todos `exit 0`):
- Matrículas 15/15 · Responsables 20/20 · Configuración raíz 8/8
- Ciclos 6/6 · Estructura-académica 3/3 · Conceptos-financieros 3/3 · Planes-pago 5/5

Los specs son 100% lógica-only (sin hooks DOM), salvo hooks de texto/aria conservados
donde existían (p.ej. 'No hay grados configurados.', 'Nuevo grado', `role="status"`).

## Validación
- Suite FE completa, production build, coverage gate y `git diff --check`: ver resultado en el reporte del PR.

## Restricciones respetadas
- Conventional Commits en español; commits pequeños por módulo.
- Sin estilos paralelos: todos los primitivos vienen de `styles.css`.
- Sin cambios de lógica de negocio en ningún módulo.
- Sin backend/DB/pagos/cargos/portal-responsable.
- No se mergeó: el PR contra main queda para revisión humana.

## Pendientes reales para 026
- Migrar `/cargos` y `/pagos` a la foundation (bloque financiero siguiente).
- Deuda #10 (páginas de negocio que consultan Supabase directo: `alumnos`,
  `matriculas`, `configuracion/ciclos`, `configuracion/estructura-academica`) —
  migrar a API .NET en bloque dedicado post-UX.
- Módulos financieros (`pagos.css`) explícitamente fuera de alcance de 025.

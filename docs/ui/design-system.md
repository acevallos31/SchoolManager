# SchoolManager — Design System (Bloque 024)

> Fuente única de verdad visual del frontend. Los tokens y primitivas viven en
> `frontend/schoolmanager-frontend/src/styles.css`. Consumir tokens vía
> `var(--sm-…)`; **no repetir valores visuales hardcodeados por página**.
> Dirección de diseño inspirada en SaaS administrativo moderno (referencia de
> calidad: Vertic — se toma dirección/calidad, **no** se copia HTML/CSS).

## 1. Principios

- **SaaS administrativo moderno**: limpio, profesional, buena densidad de
  información, jerarquía consistente.
- **Fuente única de verdad**: todos los valores visuales salen de los tokens.
- **Escalable por adopción**: la foundation se aplica hoy a AppShell, Dashboard
  y `/alumnos` (piloto); el resto migra sin rediseñarse desde cero.
- **Sin dependencias pesadas**: CSS plano + Angular actuales; sin biblioteca UI,
  sin preprocesador, sin fuente descargada (usa el stack del sistema).
- **Sin tocar lógica de negocio**: solo presentación.

## 2. Design tokens

Definidos en `:root` de `styles.css`.

| Grupo | Tokens |
|---|---|
| Paleta | `--sm-primary`, `--sm-primary-hover`, `--sm-primary-strong`, `--sm-primary-soft`, `--sm-accent`, `--sm-accent-hover` |
| Superficies/fondos | `--sm-bg`, `--sm-bg-soft`, `--sm-surface`, `--sm-surface-muted`, `--sm-surface-sunken`, `--sm-inverse`, `--sm-inverse-soft` |
| Texto | `--sm-text`, `--sm-text-strong`, `--sm-text-soft`, `--sm-text-muted`, `--sm-text-on-primary`, `--sm-text-on-inverse` |
| Bordes | `--sm-border`, `--sm-border-strong`, `--sm-border-focus` |
| Estados | `--sm-success`, `--sm-success-soft`, `--sm-warning`, `--sm-warning-soft`, `--sm-error`, `--sm-danger`, `--sm-error-soft`, `--sm-info`, `--sm-info-soft` |
| Spacing | `--sm-space-1` (0.25rem) … `--sm-space-7` (3rem), escala 4px |
| Radius | `--sm-radius-sm|md|lg|xl|pill` |
| Sombras | `--sm-shadow-sm|md|lg` |
| Tipografía | `--sm-font`, `--sm-font-mono`, `--sm-fs-xs|sm|md|lg|xl|2xl`, `--sm-lh-tight|normal`, `--sm-weight-regular|medium|semibold|bold` |
| Controles | `--sm-control-h` (2.375rem), `--sm-control-h-sm` (1.875rem) |
| Layout | `--sm-sidebar-w` (264px), `--sm-topbar-h` (60px) |
| Breakpoints | `--sm-bp-sm` (640px), `--sm-bp-md` (900px), `--sm-bp-lg` (1200px) |
| Z-index | `--sm-z-dropdown` (100) → `--sm-z-toast` (600) |

### Paleta (semántica)

- **Primario** `#0f5fa8` — acción principal, navegación activa, enlaces.
- **Acento** `#b76f2a` — acentos cálidos (eyebrow, highlights), refuerza
  la identidad institucional.
- **Fondo** `#f4efe7` cálido → gradiente suave, para evitar el "template
  escolar genérico".
- **Inverso** `#132b47` (azul marino) — topbar/sidebar.

## 3. Tipografía y jerarquía

| Rol | Clase | Tamaño | Uso |
|---|---|---|---|
| Page title | `.sm-page-title` | `--sm-fs-2xl` (1.75rem), bold | Encabezado de página |
| Section title | `.sm-section-title` | `--sm-fs-xl` (1.375rem), semibold | Título de card/sección |
| Eyebrow | `.sm-eyebrow` | `--sm-fs-xs`, uppercase, accent | Subtítulo de metadato |
| Body | `.sm-body` | `--sm-fs-md` (0.9375rem) | Texto principal |
| Helper | `.sm-helper` | `--sm-fs-sm`, text-soft | Notas de ayuda |
| Label | `.sm-label` | `--sm-fs-sm`, semibold | Etiquetas de formulario |
| Table text | `.sm-table` td | `--sm-fs-sm` (0.8125rem) | Celdas de tabla |
| Metadata | `.sm-metadata` | `--sm-fs-xs`, text-muted | Fechas, ids, detalles |

Sin dependencia externa: stack del sistema (Segoe UI / Inter / system-ui).

## 4. Componentes base

| Patrón | Clase(s) | Notas |
|---|---|---|
| Botón primario | `.sm-btn.sm-btn--primary` | Acción principal; `:hover` más oscuro |
| Botón secundario | `.sm-btn.sm-btn--secondary` | Acción neutra con borde |
| Botón ghost | `.sm-btn.sm-btn--ghost` | Acción sutil (texto + fondo suave hover) |
| Botón peligro | `.sm-btn.sm-btn--danger` | Acción destructiva |
| Botón pequeño | añade `.sm-btn--sm` | Tablas, filas, detalle |
| Enlace-acción | `.sm-link-action` | Botón con look de enlace |
| Card | `.sm-card` + `.sm-card__header`/`__body` | Superficie de contenido |
| Badge estado | `.sm-badge.sm-badge--success/warning/error/info/neutral` | Estados de registro |
| Input/Select/Textarea | `.sm-input/.sm-select/.sm-textarea` | Controls de form; `.sm-field` wrapper + `.sm-label`; `.sm-field__error` |
| Tabla | `.sm-table-wrap` + `.sm-table` | Contenedor scroll-x + tabla; `.sm-table th/td` |
| Estado vacío | `.sm-state` + `.sm-state__title/__body` | Empty state |
| Loading | `.sm-spinner` (+ `.sm-spinner--sm`) | Spinner |
| Alerta | `.sm-alert--success/--error` | Mensajes de resultado |
| Modal | `.sm-modal-overlay` + `.sm-modal` (+ `.sm-modal__title`/`__detail`/`__actions`) | Diálogo; overlay cierra al hacer clic fuera; `__actions` = fila derecha de botones (Cancelar/Confirmar) |
| Tabs | `.sm-tabs` | Pestañas de sección (extraído de portal-padre 024); active state vía clase, hover `0.15s ease` |

Regla: extraer primitivas **solo cuando hay uso real/inmediato** — no crear una
biblioteca gigantesca por adelantado.

## 5. AppShell global

El AppShell (`layout/app-shell/`) envuelve **todas** las rutas admin
(dashboard, alumnos, matriculas, responsables, cargos, pagos, configuración y
subvistas) como rutas hijas. Quedan fuera del shell: `/login` y `/portal-padre`
(por diseño).

- **Topbar** fija (`--sm-topbar-h`): marca + botón hamburguesa (solo móvil/tablet).
- **Sidebar** (`--sm-sidebar-w`): persistente en escritorio; en móvil/tablet se
  convierte en **drawer deslizante con overlay**.
- **Navegación activa**: `esRutaActiva()` — el Panel solo en ruta exacta; el
  resto por prefijo.
- **Filtrado por permisos**: `mostrarItem()` oculta enlaces sin permiso;
  el Panel (dashboard) es visible para autenticados y Matrículas requiere el
  permiso `academico.matriculas.ver` (el resto de rutas filtra por su propio permiso).
- **Identidad**: muestra **roles reales** del `UsuarioActual`; nunca inventa
  nombre/email/institución (el backend solo expone roles/permisos).
- **Logout** → navega a `/login`.

### Responsive

- **Desktop (≥ `--sm-bp-lg`)**: sidebar fijo + contenido amplio.
- **Tablet**: sidebar colapsa a drawer; hamburguesa visible.
- **Móvil**: drawer con overlay; cierre al navegar o con el botón.
- **Tablas**: `.sm-table-wrap` da scroll-x horizontal sin overflow de página.

## 6. Adopción (estado real)

- **Aplicado**: AppShell global, Dashboard (sin sidebar duplicado), `/alumnos`
  como pantalla piloto (workspace master/detalle + acciones reales).
- **Migrado en 025**: `/matriculas`, `/responsables`, `/configuracion` (raíz),
  y los submódulos `/configuracion/ciclos`, `/configuracion/estructura-academica`,
  `/configuracion/conceptos-financieros`, `/configuracion/planes-pago` — todos
  consumen tokens/primitivas de la foundation (botones, cards, tablas, badges,
  inputs, modal, tabs), sin estilos paralelos.
- **Migrado en 026**: `/cargos` y `/pagos` (bloque financiero) consumen la
  foundation; los KPIs de resumen quedan como maquetación local por página
  (mismo criterio que `.sm-stats` del Dashboard).
- **Migrado en 027**: `/portal-padre` (portal responsable, solo lectura) consume
  la foundation — cabecera de marca propia fuera del AppShell, selector de alumno
  con `sm-btn`, KPIs con `sm-card`, `sm-tabs`, tablas `sm-table`, badges `sm-badge`
  por estado y estados `sm-state`/`sm-spinner`/`sm-alert`; sin estilos paralelos.
- **Pendiente de migrar**: ninguna página pendiente por ahora; las que aún
  consultan Supabase directo son deuda registrada en `technical-debt.md` (#10),
  no bloqueo de adopción visual.
- Regla de adopción: cada pantalla nueva consume tokens/primitivas; nada de
  valores hardcodeados ni sidebar duplicado.

## 7. Reglas de calidad

- Sin estilos inline ni `!important` como solución general.
- No copiar CSS página por página: extraer a primitiva global cuando hay uso
  real en ≥2 lugares.
- Sin componentes monolíticos; sin animaciones excesivas.
- Media queries: usar tokens de breakpoint, no valores sueltos.

## 8. Decisiones de diseño (inspiradas en Vertic, sin copiarlo)

- **Fondo cálido con gradiente** en vez de gris frío genérico → identidad propia
  y aspecto SaaS cuidado.
- **Topbar + sidebar oscuros** (`--sm-inverse`) para anclar la marca; contenido
  claro sobre superficie blanca para densidad legible.
- **Acento ámbar** (`--sm-accent`) como color distintivo frente al azul primario
  estándar — evita el look "plantilla escolar".
- **Drawer móvil con overlay** en lugar del colapso a columna que existía <900px.
- **Tokens espaciados en escala 4px** para ritmo vertical consistente entre
  pantallas.

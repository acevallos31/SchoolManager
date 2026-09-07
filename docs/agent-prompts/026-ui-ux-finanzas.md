# Prompt operativo — Bloque 026: UI/UX Finanzas

## Objetivo
Migrar visualmente `/cargos` y `/pagos` a la foundation UI creada en 024 y extendida en 025, sin alterar lógica de negocio, backend, DB, migraciones, RPC ni contratos API.

## Lectura obligatoria antes de cambiar código
Leer completos, en este orden:
1. `AGENTS.md`
2. `docs/AI_CONTEXT.md`
3. `docs/ui/design-system.md`
4. `docs/handoffs/024-ui-ux-foundation.md`
5. `docs/handoffs/025-ui-ux-matriculas-responsables-configuracion.md`

El repositorio es la fuente de verdad. No reconstruir historia desde chats anteriores.

## Estado base esperado
- PR #48 / Bloque 025 ya mergeado a `main`.
- Merge commit esperado de 025: `38fe12818036e33065eb25fa5d78d4102962b986`.
- Rama de trabajo ya creada: `feature/ui-ux-finanzas-026`.

Antes de editar:
- cambiar a `main` y actualizar con `git pull --ff-only`;
- confirmar working tree limpio;
- verificar SHA real de `main`;
- confirmar que 025 está presente;
- cambiar a `feature/ui-ux-finanzas-026`;
- si la rama quedó detrás de `main`, actualizarla de forma segura sin reescrituras innecesarias;
- reportar síntesis corta del estado real antes de comenzar.

## Alcance exacto
Trabajar solamente en:
- `/cargos`
- `/pagos`
- estilos/primitivas compartidas solo si aparece un patrón realmente reusable;
- tests frontend relacionados;
- documentación de UI/contexto/handoff.

## Regla principal
Reusar estrictamente la foundation existente. Priorizar:
- `--sm-*`
- `.sm-btn*`
- `.sm-card*`
- `.sm-badge*`
- `.sm-input`
- `.sm-select`
- `.sm-table`
- `.sm-alert*`
- `.sm-state*`
- `.sm-spinner`
- `.sm-modal*`
- `.sm-tabs`

No crear estilos paralelos por página si ya existe un patrón equivalente.

Si falta un patrón que aparezca en 2 o más lugares y sea claramente reusable, extender la foundation con el cambio mínimo y documentarlo en `docs/ui/design-system.md`.

## Fase 0 — Auditoría visual y funcional
Antes de reescribir HTML/CSS:
- revisar `cargos.html/.css/.ts/.spec.ts`;
- revisar `pagos.html/.css/.ts/.spec.ts`;
- identificar clases/hook usados por tests;
- identificar estados loading/error/empty/success;
- identificar modales/confirmaciones/formularios/tablas;
- detectar estilos duplicados que ya cubra la foundation;
- verificar el warning conocido de budget de `pagos.css`.

No modificar lógica durante esta auditoría.

## Cargos
Migrar visualmente conservando comportamiento real.

Priorizar:
- encabezado y acciones;
- búsqueda/filtros existentes;
- tabla/listado;
- alumno/contexto;
- monto original;
- saldo derivado;
- total aplicado;
- vencimiento;
- estados reales: `pendiente`, `parcial`, `pagado`, `anulado`;
- `vencido` únicamente como indicador derivado cuando corresponda;
- creación/anulación si esas acciones existen en la UI actual;
- loading/error/empty;
- responsive.

No modificar cómo se calcula saldo ni estados. No añadir estado persistido `vencido`.

## Pagos
Migrar visualmente conservando exactamente el flujo funcional de 021.

Priorizar:
- contexto del alumno;
- historial de pagos;
- registro de pago;
- monto total;
- referencia externa;
- responsable/pagador cuando aplique;
- distribución/aplicaciones a uno o varios cargos;
- importes parciales;
- detalle de aplicaciones;
- estado registrado/anulado;
- anulación con motivo;
- feedback de operaciones;
- prevención de doble submit;
- responsive.

No cambiar reglas de sobrepago, validaciones de responsable, aplicaciones, reversión ni atomicidad.

## Manejo UX de errores
Aplicar el patrón vigente:
- loading visible;
- error visible y seguro;
- empty state diferente de error;
- success feedback cuando aporte valor;
- retry donde tenga sentido;
- acciones deshabilitadas mientras procesan;
- no mostrar excepciones crudas de API/Supabase;
- no filtrar SQL, tokens, stack traces ni detalles internos.

No crear una arquitectura transversal nueva de excepciones.

## Microinteracciones
Solo ligeras y funcionales:
- apertura/cierre de modal;
- tabs;
- alertas;
- loading;
- hover/focus;
- expand/collapse si ya existe.

Respetar `prefers-reduced-motion`.
No agregar librerías pesadas de animación.

## Responsive
Verificar desktop, tablet y móvil.
Evitar overflow horizontal injustificado.
Para tablas densas, reutilizar/extender un patrón responsive reusable en vez de soluciones ad hoc.

## Warning de budget de pagos.css
Existe un warning heredado: `pagos.css` excedía el budget por ~45 bytes.

Como 026 sí toca ese módulo:
- reducir duplicación usando la foundation;
- intentar dejar el warning resuelto naturalmente;
- NO aumentar budgets solo para ocultarlo;
- NO minificar manualmente CSS a costa de legibilidad.

## Tests
Mantener todos los tests existentes.
Agregar tests únicamente para comportamiento UX/lógica real, por ejemplo:
- loading;
- error;
- success;
- acciones condicionales;
- detalle de aplicaciones;
- anulación;
- prevención de doble submit;
- estados visuales derivados mediante funciones TS cuando aplique.

No hacer tests frágiles de píxeles, colores o CSS exacto.

## Restricciones duras
NO tocar:
- backend;
- DB;
- migraciones;
- RPC;
- contratos API;
- lógica financiera;
- portal responsable;
- deuda técnica #10;
- Figma;
- producción;
- Bloque 027.

Si aparece una necesidad real de backend/DB, detenerse y reportarla antes de continuar.

## Commits
Trabajar con commits pequeños y coherentes. Orden recomendado:
1. `feat(ui): adopta foundation en cargos`
2. `feat(ui): adopta foundation en pagos`
3. foundation adicional, solo si realmente fue necesaria
4. tests
5. docs

Antes de cada commit importante:
- revisar `git diff`;
- excluir archivos fuera de alcance;
- correr tests del módulo correspondiente.

## Protección ante límite de contexto
Si la ventana de contexto se acerca al límite:
- detener cambios grandes;
- no dejar trabajo importante solo en working tree;
- hacer checkpoint únicamente de código coherente/testeado;
- commit;
- push;
- actualizar el handoff con estado real;
- reportar qué está terminado y qué falta.

No continuar a ciegas después de compresión/contexto saturado.

## Documentación
Crear:
- `docs/handoffs/026-ui-ux-finanzas.md`

Actualizar:
- `docs/AI_CONTEXT.md`
- `docs/ui/design-system.md` solo si se extendió la foundation.

Documentar únicamente el estado real.

## Validación final obligatoria
Antes del PR:
- suite FE completa;
- production build;
- coverage gate;
- `git diff --check`;
- revisión manual responsive cuando sea posible.

El objetivo adicional es que el warning de `pagos.css` desaparezca si la migración a la foundation lo permite naturalmente.

## Git / PR
- Push de `feature/ui-ux-finanzas-026`.
- Abrir PR contra `main`.
- NO mergear.
- NO iniciar 027.

## Reporte final
Reportar:
- número y enlace del PR;
- head SHA;
- commits;
- archivos principales;
- tests FE;
- coverage;
- production build;
- warnings;
- `git diff --check`;
- mergeability;
- riesgos residuales reales.

## Criterio de éxito
Al terminar 026, `/cargos` y `/pagos` deben verse y comportarse como parte del mismo producto visual definido en 024/025, conservando íntegramente las reglas financieras implementadas en 020/021 y sin introducir deuda visual paralela.

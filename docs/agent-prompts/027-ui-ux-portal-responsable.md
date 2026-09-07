# Prompt operativo — Bloque 027: UI/UX Portal Responsable

## Objetivo
Migrar visualmente `/portal-padre` a la foundation UI `sm-*` consolidada en los Bloques 024–026, preservando íntegramente el flujo funcional del Portal Responsable y manteniéndolo separado del AppShell administrativo.

## Lectura obligatoria antes de cambiar código
Leer completos, en este orden:
1. `AGENTS.md`
2. `docs/AI_CONTEXT.md`
3. `docs/ui/design-system.md`
4. `docs/handoffs/024-ui-ux-foundation.md`
5. `docs/handoffs/025-ui-ux-matriculas-responsables-configuracion.md`
6. `docs/handoffs/026-ui-ux-finanzas.md`

El repositorio es la fuente de verdad. No reconstruir historia desde chats anteriores.

## Estado base esperado
- PR #49 / Bloque 026 mergeado a `main`.
- Merge commit esperado de 026: `5acab121f655046d0ec7b970d0b741bad5d1fa0c`.
- Rama de trabajo ya creada: `feature/ui-ux-portal-responsable-027`.

Antes de editar:
- cambiar a `main` y ejecutar `git pull --ff-only`;
- confirmar working tree limpio;
- verificar SHA real de `main`;
- confirmar que 026 está presente;
- cambiar a `feature/ui-ux-portal-responsable-027`;
- si la rama quedó detrás de `main`, actualizarla de forma segura sin reescrituras innecesarias;
- reportar una síntesis corta del estado real antes de comenzar.

## Alcance exacto
Trabajar solamente en:
- `/portal-padre` y componentes/estilos directamente asociados;
- tests frontend relacionados;
- documentación del bloque;
- foundation compartida solo si aparece un patrón verdaderamente reusable y no cubierto.

No modificar el AppShell administrativo para “meter” el portal dentro. El Portal Responsable debe seguir aislado del shell admin.

## Contexto funcional que debe preservarse
El Bloque 022 dejó el Portal Responsable read-only y con superficie aislada para responsables.

Flujo esperado:
1. usuario responsable inicia sesión;
2. accede a sus alumnos vinculados reales;
3. selecciona un alumno;
4. visualiza resumen financiero;
5. visualiza cargos;
6. visualiza pagos.

No agregar pago en línea ni acciones financieras de administración.
No convertir el portal en CRUD.
No debilitar permisos ni reutilizar endpoints administrativos para simplificar la UI.

## Regla principal de diseño
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
- `.sm-eyebrow`
- `.sm-section-title`
- `.sm-helper`

No crear una segunda identidad visual para el portal. Debe sentirse como el mismo producto, pero con navegación y jerarquía apropiadas para un responsable, no para un administrador.

## Fase 0 — Auditoría visual y funcional
Antes de reescribir HTML/CSS:
- localizar archivos reales del portal (`portal-padre.html/.css/.ts/.spec.ts` o equivalentes);
- revisar sus servicios/dependencias solo para entender contrato y estados, sin modificarlos salvo necesidad estrictamente frontend;
- identificar estados loading/error/empty;
- identificar selección de alumno/hijo;
- identificar resumen financiero;
- identificar cargos y pagos;
- identificar logout o navegación propia;
- identificar hooks/clases que usan tests;
- identificar textos/errores crudos que deban mapearse de forma segura;
- detectar CSS duplicado que ya cubra la foundation.

No modificar lógica durante esta auditoría.

## Estructura visual esperada
Sin imponer una maqueta artificial, procurar una jerarquía clara y simple:

### Encabezado del portal
- identidad SchoolManager;
- contexto de “Portal Responsable”;
- acción real de cerrar sesión si ya existe;
- sin sidebar de administración;
- sin navegación a módulos admin.

### Selección de alumno
- selector/tarjetas/tabs según lo que ya exista y tenga menos fricción;
- usar nombres reales entregados por la API;
- no inventar datos de institución/persona;
- estado vacío claro si no hay alumnos vinculados.

### Resumen financiero
- cards/KPIs reutilizando `sm-card` + `sm-eyebrow`;
- saldos/montos legibles;
- no recalcular reglas de negocio en la vista si ya vienen del contrato;
- conservar semántica de estados.

### Cargos
- tabla/listado con `sm-table` + `sm-table-wrap`;
- badges semánticos para `pendiente`, `parcial`, `pagado`, `anulado`;
- `vencido` solo como indicador derivado si la superficie actual ya lo expone;
- no agregar botones de “Cobrar”, “Anular”, “Editar” ni otras acciones administrativas.

### Pagos
- historial read-only;
- recibo, monto, fecha, método/estado si el contrato lo provee;
- detalle solo si ya existe funcionalmente;
- no agregar registro/anulación de pagos.

## UX de estados
Aplicar patrón vigente:
- loading visible;
- error visible, seguro y comprensible;
- empty distinto de error;
- retry donde tenga sentido y ya sea viable con la lógica existente;
- acciones deshabilitadas mientras procesan;
- no mostrar stack traces, SQL, tokens, respuestas crudas de Supabase/API ni detalles internos.

Si el portal ya tiene errores correctamente mapeados, conservar la lógica y mejorar solo presentación.

## Responsive
Verificar desktop, tablet y móvil.
Prioridades:
- selector de alumnos usable en móvil;
- tarjetas financieras sin cortes;
- tablas con scroll contenido en `sm-table-wrap` cuando corresponda;
- ningún overflow de página;
- botones y controles con targets cómodos;
- textos largos/montos no deben romper el layout.

## Accesibilidad básica
Sin convertir 027 en el bloque final de accesibilidad (eso queda para 028), asegurar al menos:
- labels/aria existentes no se pierden;
- contraste usando tokens existentes;
- `role="alert"`/`status` cuando corresponda;
- foco visible según foundation;
- botones reales para acciones;
- encabezados en orden razonable;
- modales, si existen, conservan `role="dialog"` y `aria-modal`.

## Microinteracciones
Solo ligeras y funcionales:
- selección de alumno;
- loading;
- alertas;
- tabs si ya se justifican;
- modal/detalle si ya existe;
- hover/focus.

Respetar `prefers-reduced-motion`.
No agregar librerías pesadas.

## Restricciones duras
NO tocar:
- backend;
- DB;
- migraciones;
- RPC;
- contratos API;
- permisos/RLS;
- lógica financiera;
- AppShell admin salvo un bloqueo real externo al portal;
- deuda técnica #10;
- Figma;
- producción;
- Bloque 028.

Si aparece una necesidad real de backend/DB/auth/permiso, detenerse y reportarla antes de continuar.

## Tests
Mantener todos los tests existentes.
Agregar tests solo si hay comportamiento real frontend que lo amerite, por ejemplo:
- loading/error/empty;
- selección de alumno;
- cambio de alumno actualiza la vista;
- logout si esa lógica se toca;
- render condicional de cargos/pagos;
- estados seguros.

No crear tests de píxeles, colores o CSS exacto.
No reescribir tests funcionales solo para adaptarlos a clases CSS salvo que el test dependiera incorrectamente de una clase visual.

## Revisión visual
Idealmente hacer revisión real en navegador.
Si la máquina no dispone de credenciales/staging/datos para flujo autenticado, está permitido el mismo criterio aprobado en 026:
- renderizar templates reales + CSS real + foundation real;
- usar datos representativos únicamente para inspección visual;
- revisar desktop/tablet/móvil;
- documentar explícitamente que fue revisión estática de layout, no E2E autenticado.

Nunca inventar credenciales ni tocar producción para conseguir una captura.

## Commits
Trabajar con commits pequeños y coherentes. Orden sugerido:
1. `feat(ui): adopta foundation en portal responsable`
2. tests, solo si hubo cambios de comportamiento/UX que los justifiquen
3. docs

Antes de cada commit importante:
- revisar `git diff`;
- excluir archivos fuera de alcance;
- ejecutar tests relevantes.

## Protección ante límite de contexto
Si la ventana de contexto se acerca al límite:
- detener cambios grandes;
- dejar el working tree en estado coherente;
- hacer checkpoint de código testeado;
- commit;
- push;
- actualizar handoff con estado real;
- reportar exactamente qué quedó terminado y qué falta.

No continuar a ciegas después de compresión/contexto saturado.

## Documentación
Crear:
- `docs/handoffs/027-ui-ux-portal-responsable.md`

Actualizar:
- `docs/AI_CONTEXT.md`
- `docs/ui/design-system.md` solo si se añadió un patrón reusable real.

Documentar solo el estado real.

## Validación final obligatoria
Antes del PR:
- tests específicos del portal;
- suite FE completa;
- production build;
- coverage gate;
- `git diff --check`;
- revisión visual desktop/tablet/móvil cuando sea posible.

Si hay warnings, reportarlos y distinguir si son nuevos o preexistentes.

## Git / PR
- Push de `feature/ui-ux-portal-responsable-027`.
- Abrir PR contra `main`.
- NO mergear.
- NO iniciar 028.

## Reporte final
Reportar:
- número y enlace del PR;
- head SHA;
- commits;
- archivos principales;
- resultado visual;
- tests específicos;
- suite FE;
- coverage;
- production build;
- warnings;
- `git diff --check`;
- mergeability;
- riesgos residuales reales.

## Criterio de éxito
Al terminar 027, el Portal Responsable debe verse como parte del mismo producto definido en 024–026, con una experiencia clara y coherente para padres/responsables, manteniendo su aislamiento del área administrativa y preservando exactamente su naturaleza read-only y sus reglas de acceso/finanzas.

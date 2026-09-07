# SchoolManager - AI Context

## Arquitectura
- Angular frontend, .NET backend y PostgreSQL/Supabase con JWT Supabase.
- Monolito modular, UUID internos, RLS y RPC para escrituras críticas.
- Evitar CQRS, MediatR, microservicios, Generic Repository y UnitOfWork artificial.
- Permisos RBAC, no checks hardcodeados por rol y no DELETE físico de históricos.
- **Login/autenticación por diseño**: el frontend autentica contra **Supabase Auth**
  (`auth.ts`) y el backend .NET valida el JWT resultante. El backend solo expone
  `GET /auth/me` (información de sesión/permisos); no existe ni debe existir un
  `POST /api/auth/login` en .NET.

## Contexto institucional
- Modo single resuelve la institución activa; modo multi exige contexto explícito.
- El selector global multiinstitución está pendiente.

## Migraciones (todo mergeado a main)
- 001-008 RBAC y modelo académico base; 009 RLS y RPC; 010 identidad; 011 creación
  alumno con documento; 012 configuración de implementación; 013 centro educativo;
  014 ciclos/períodos; 015 períodos anticipados; 016 grados, jornadas y secciones.
- 017 responsables (gestión RPC): en `main` (PR #33/#35).
- 018 configuración financiera (conceptos y planes de pago): en `main`.
- 019 cargos/obligaciones generadas a partir de planes 018: en `main`
  (PR #38, `08a08b1`).
- 020 grados y jornadas **por institución** (multiinstitución): en `main`
  (PR #42, `cf1cd5e`). Añade `institucion_id` a `grados`/`jornadas`.
- 021 pagos/cobranza: en `main` (PR #44, `f61c931`).
- 022 portal responsable (lectura): en `main` (PR #45, `bed6a85`).

## Estándares de ingeniería
- Los **12 principios de ingeniería** del proyecto viven en `docs/engineering-principles.md` y son reglas vinculantes citables por número (`#1`…`#12`) en PRs, handoffs y decisiones técnicas.
- La IA no sustituye la revisión: todo output generado por IA se lee y verifica antes de integrarse (#12).

## Módulo Responsables (017/018)
- Namespace vigente: **`academico.responsables.*`** (usado por RLS 009 y backend `Permisos.cs`); `responsables.responsables.*` (007) es legacy sin uso, conservado.
- MIG017: 9 RPC `security definer` (crear con/para persona, editar, inactivar/reactivar, vincular, editar vinculo, desactivar/reactivar vinculo) con invariantes persona+institución único, principal único por alumno, no-cruce de institución, sin DELETE físico. Superficie solo-RPC.
- Backend: `ResponsablesController` (API `api/responsables`, paginado `PaginatedResult`). **Cuidado**: los parámetros nullable a `AddWithValue` deben usar `?? DBNull.Value`.
- Frontend: página `/responsables` + servicio `responsables.service.ts` (API .NET) + integración Alumno→Responsable.

## Módulo Cargos (019/020)
- Migración **019**: tabla `cargos` (obligaciones materializadas desde planes/cuotas de 018) + `matriculas.plan_pago_id` + 6 RPC `security definer`. Permisos: `academico.cargos.{ver,generar,anular}`.
- **`vencido` es DERIVADO por fecha**; `pagado`/`parcial` se sincronizan con 021 vía triggers.
- Backend `CargosController` (`api/cargos`): listar por matrícula/alumno, resumen financiero, asignar plan, generar, anular. Institución desde claim `sub`; nullables `?? DBNull.Value`. **Lección**: RPC `RETURNS TABLE` con fila única agregada colapsa a `record` con `select public.rpc_(…)` → usar SIEMPRE `select * from public.rpc_(…)`.
- Frontend: página `/cargos` (lazy, contextual `alumnoId`, acceso desde Alumnos) + `cargos.service.ts` (API .NET). La página legacy `mensualidades` fue retirada.

## Módulo Pagos / Cobranza (021) — en main
- Migración **021**: `pagos` (cabecera) + `pagos_aplicaciones` (detalle), 1 pago → varios cargos; `alumno_id` obligatorio, `responsable_id` opcional validado en contexto, `referencia_externa` opcional única por institución. Superficie solo-RPC.
- **Saldo SIEMPRE derivado** = `monto_original − SUM(aplicaciones vigentes)`, nunca almacenado; `monto_total` = Σ aplicaciones vigentes; sin sobrepago.
- `cargos.estado` → `pendiente|parcial|pagado|anulado` sincronizado por **triggers en la DB**. Anulación atómica con trazabilidad, sin DELETE físico.
- Permisos `academico.pagos.{ver,registrar,anular}` (iniciales solo admin).
- Backend: `PagosController` (`/api/pagos`), `PagoDto`; `CargoDto`/`ResumenFinancieroDto` extendidos.
- Frontend: página `/pagos` (lazy, contextual `alumnoId`) + `pagos.service.ts`; `/cargos` muestra saldo derivado y estados parcial/pagado. Detalle en `docs/handoffs/021-pagos-cobranza.md`.

## Módulo Portal Responsable (022) — en main
- Migración **022**: superficie de **lectura** para responsables: `rpc_mis_alumnos_responsable`, `rpc_cargos_responsable`, `rpc_resumen_financiero_responsable`, `rpc_pagos_responsable`, `rpc_pago_aplicaciones_responsable`. Guard por identidad (usuario→responsable→`alumno_responsable` activo + `acceso_financiero=true`), no por permiso admin.
- Backend: `PortalResponsableController` (`/api/portal-responsable`), DTOs `MisAlumnoDto`/`CargoDto`/`PagoDto` (nullables `string | null` alineados con `Guid?` .NET).
- Frontend: página `/portal-padre` reescrita (150 líneas TS, 209 HTML, 196 CSS) contra la **API .NET** en solo lectura, **sin** botón de pago. `PortalResponsableService` centraliza el acceso (errores con causa, 3 estados en aplicaciones: error/cargando/vacío). Detalle en `docs/handoffs/022-portal-responsable.md`.

## Modelo académico
Institución -> Ciclo -> Período matrícula -> Grado -> Jornada opcional -> Sección -> Matrícula -> Alumno.

- Los períodos pueden ser anticipados, normales o extraordinarios y sus fechas son independientes de las académicas.
- **Grados y jornadas son por institución** (020); secciones por institución/ciclo.
- Una sección con matrículas no cambia ciclo, grado ni jornada.

## Estado frontend
- **Foundation visual (Bloque 024, mergeado a `main` como `e876ae1`, PR #47)**:
  design tokens `--sm-*` como fuente única de verdad en `src/styles.css` +
  primitivas globales (`.sm-btn`, `.sm-card`, `.sm-badge`, `.sm-table`,
  `.sm-input`, `.sm-state`, `.sm-spinner`, `.sm-alert`, tipografía jerárquica).
  Doc: `docs/ui/design-system.md`.
- **AppShell global**: envuelve **todas** las rutas admin (dashboard, alumnos,
  matriculas, responsables, cargos, pagos, configuracion + subvistas) como hijos;
  topbar + sidebar persistente en escritorio y **drawer móvil con overlay**;
  navegación filtrada por permisos (`mostrarItem`); identidad con **roles reales**
  (no inventar nombre/institución). `/login` y `/portal-padre` quedan fuera del shell.
  El Panel (dashboard) es visible para autenticados; **Matrículas exige
  `academico.matriculas.ver`**.
- **Foundation aplicada a**: AppShell, Dashboard (sin sidebar duplicado),
  `/alumnos` (piloto master/detalle).
- **Adoptado en 025 (PR #48, mergeado en `main`)**:
  `/matriculas`, `/responsables`, `/configuracion` (raíz) y los submódulos
  `/configuracion/ciclos`, `/configuracion/estructura-academica`,
  `/configuracion/conceptos-financieros`, `/configuracion/planes-pago` — todos
  consumen tokens/primitivas (botones, cards, tablas, badges, inputs, modal, tabs)
  sin estilos paralelos ni cambio de lógica de negocio.
- **Adoptado en 026 (rama `feature/ui-ux-finanzas-026`)**: `/cargos` y `/pagos`
  (bloque financiero) migrados a la misma foundation `sm-*`. Los KPIs de resumen
  quedan como maquetación local por página (mismo criterio que `.sm-stats` del
  Dashboard); se resolvió el warning de budget de `pagos.css` (duplicación de
  foundation eliminada). Sin cambio de lógica funcional ni backend/DB.
- Módulos navegables (rutas lazy): `/configuracion`, `/configuracion/ciclos`,
  `/configuracion/estructura-academica`, `/configuracion/conceptos-financieros`,
  `/configuracion/planes-pago`, `/responsables`, `/matriculas`, `/alumnos`,
  `/cargos`, `/pagos` (contextuales desde Alumnos/Cargos), y `/portal-padre`
  (responsable, solo lectura).
- Guard de navegación: `PermissionGuard` (lee `route.data['permiso']`); solo las
  rutas autenticadas. `AdminGuard`/`PadreGuard` fueron **eliminados** (código
  muerto, 023) — no reintroducirlos.
- **Páginas de negocio que aún consultan Supabase directo** (sin controller .NET
  equivalente, flujo real que funciona contra Supabase): `alumnos`, `matriculas`
  (listado), `configuracion/ciclos` y `configuracion/estructura-academica`, vía
  `alumno.service.ts`, `ciclo-escolar.service.ts`, `estructura-academica.service.ts`
  y `configuracion.service.ts`. Migrarlas a la API .NET es deuda pendiente
  registrada en `docs/technical-debt.md` (ver #10), NO reescribir de forma
  aislada: se aborda en un bloque dedicado.

## Git y validación
Las reglas operativas de Git, migraciones y validación están en `AGENTS.md`.
Estado real del repo: 001-022 en `main`; **023 (cierre funcional pre-UX) mergeado
en `main` (`702d2f1`, PR #46)**. Bloque **024 (UI/UX foundation + AppShell)
mergeado en `main` (`e876ae1`, PR #47)**. Bloque **025 (adopción foundation en
matrículas/responsables/configuración) mergeado en `main` (`38fe128`, PR #48)**.
Bloque **026 (adopción foundation en cargos/pagos, bloque financiero) en la rama
`feature/ui-ux-finanzas-026`**, PR contra main pendiente de revisión humana
(no mergear). **Sin cambios backend/DB en 025/026; deuda #10 (Supabase directo)
sigue pendiente.**

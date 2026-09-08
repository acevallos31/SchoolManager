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
- Frontend: página `/portal-padre` reescrita contra la **API .NET** en solo lectura, **sin** botón de pago. `PortalResponsableService` centraliza el acceso (errores con causa, estados error/cargando/vacío). Detalle funcional en `docs/handoffs/022-portal-responsable.md` y cierre visual en `docs/handoffs/027-ui-ux-portal-responsable.md`.

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
- **Adoptado en 026 (PR #49, mergeado en `main` como `5acab12`)**: `/cargos` y
  `/pagos` migrados a la misma foundation `sm-*`.
- **Adoptado en 027 (PR #50, mergeado en `main` como `ba3c78fa19389d2d27ce31948bdb2a14bf6dcfe8`)**:
  `/portal-padre` migrado a la misma foundation `sm-*`; sin cambios backend/DB ni lógica funcional.
- **Bloque 028 — cierre UX final: COMPLETADO y mergeado** en PR #51 como
  `a80c19cc10520f0d2cc6c820c288db9bb28631ab`. Cerró auditoría UX, responsive,
  accesibilidad básica y limpieza CSS. Suite FE 184/184 y build OK. La revisión
  visual final fue estática; E2E autenticado quedó pendiente por falta de staging.
- Módulos navegables (rutas lazy): `/configuracion`, `/configuracion/ciclos`,
  `/configuracion/estructura-academica`, `/configuracion/conceptos-financieros`,
  `/configuracion/planes-pago`, `/responsables`, `/matriculas`, `/alumnos`,
  `/cargos`, `/pagos` (contextuales desde Alumnos/Cargos), y `/portal-padre`.
- Guard de navegación: `PermissionGuard` (lee `route.data['permiso']`); solo las
  rutas autenticadas. `AdminGuard`/`PadreGuard` fueron eliminados (023) — no reintroducirlos.
- **Páginas de negocio que aún consultan Supabase directo**: `alumnos`, `matriculas`
  (listado), `configuracion/ciclos` y `configuracion/estructura-academica`. Esta es
  deuda #10 registrada en `docs/technical-debt.md`; queda fuera del Bloque 029.

> Actualización 030F (Codex, 2026-09-07/08): la referencia anterior a páginas
> que consultan Supabase y las menciones posteriores a deuda #10 pendiente
> describen el estado previo a 030. Quedan sustituidas por el estado 030 siguiente;
> se conserva el detalle histórico de 029.

## Arquitectura API / Bloque 030 — CERRADO; deuda #10 RESUELTA (PR #53, sin merge)

- 030B Alumnos, 030C verificación de Matrículas, 030D Ciclos/Períodos y 030E
  Estructura Académica están implementados en `feature/arquitectura-api-030`.
- 030F migra las cinco RPC de `configuracion.service.ts` a `/api/configuracion`,
  incluida la consulta de contexto. **0 accesos directos Supabase de negocio**;
  única excepción productiva: `auth.ts`, Supabase Auth aprobado e intacto.
- Configuración usa permisos existentes en 012: `configuracion.sistema.editar`
  y `configuracion.instituciones.ver/editar`. La lectura acepta ver **o** editar;
  el contexto solo exige autenticación en .NET e identidad interna en RPC.
  Autorización .NET y permisos internos DB conservan sus capas separadas.
- Reglas/invariantes de configuración siguen en RPC 012/013; ninguna migración
  nueva en 030F. Migraciones 023/024 de 030D/E: aditivas, pendientes de aplicación
  manual tras revisión/CI, no aplicadas por esta sesión en Supabase.
- Suites locales: API 156/156, DB 156/156, frontend 289/289; builds correctos.
  CI y Vercel verdes en `3feb517` (run `34192575139`). SonarScanner for .NET
  con análisis C# real + TypeScript e importación Cobertura/LCOV: QG **OK**,
  New Code 86,5% cobertura / 2,7% duplicación; reglas y umbrales intactos.
- Evidencia y contratos: `docs/handoffs/030F-cierre-global.md`.

## Calidad / Bloque 029 + 029B — CERRADO (registro histórico)

Rama: `feature/calidad-sonar-e2e-029`, creada desde `main` después del merge de PR #51.

Estado final (2026-09-07): **análisis SonarCloud completo y verde** — frontend TS +
**backend C# real** (SonarScanner for .NET v11.3.0, flujo begin/build/end, tras el
029B) + coberturas Cobertura backend y LCOV frontend importadas en el mismo proyecto.
`SONAR_TOKEN` válido; `sonar-project.properties` **eliminado** (el scanner .NET no lo
lee) y propiedades pasadas como `/d:` en el `begin` (`sonar.scanner.scanAll=true`
conserva el TS; `e2e/**` excluido). Paso `sonarqube-quality-gate-action` (pineado por
SHA) tras el `end` **falla el job si el QG no es verde** — anti falso-verde extendido
al QG. **Quality Gate del PR #52 en verde** (run **34160443496**, head `6e3299a`,
`✔ Quality Gate has PASSED`); warning `C# files which cannot be analyzed...` **ausente**
(0 ocurrencias) y `.cs` realmente procesados (SchoolManager.API + IntegrationTests).
Deudas: **#7 RESUELTA**, **#9 ACTUALIZADA/RESUELTA** (QG representativo del backend).
El QG afloró y se corrigieron 2 vulns de código nuevo en el propio `deploy.yml`
(S8482/S7637) que el scanner genérico ocultaba — prueba de que el anti falso-verde
funciona. E2E: smoke 3/3 local passed; **autenticado pendiente de staging** (acción
humana #2, ver `docs/ci/e2e-auth-setup.md`). PR #52 abierto contra `main` sin merge.

Objetivo del bloque (original):
- restaurar análisis **real** de SonarCloud/quality gate en CI (el job actual puede quedar verde con `sonar-scanner` skipped cuando `SONAR_TOKEN` está vacío);
- diseñar y habilitar un entorno seguro de staging o equivalente para pruebas autenticadas;
- ejecutar E2E autenticado de los flujos críticos sin tocar producción ni usar datos reales no autorizados;
- documentar resultados, bloqueos y riesgos residuales.

Restricciones:
- no cambiar reglas de negocio, modelo de datos, migraciones ni arquitectura por conveniencia de pruebas;
- no incluir deuda #10 de Supabase directo;
- no usar producción para E2E destructivo;
- secretos/tokens se configuran fuera del repositorio; nunca se commitean.

Flujos E2E prioritarios:
1. login y `/auth/me`;
2. navegación/guards por permisos;
3. alumnos → matrícula;
4. alumno → cargos;
5. registro y consulta de pagos;
6. portal responsable read-only;
7. responsive básico con sesión real cuando el entorno lo permita.

## Git y validación
Las reglas operativas de Git, migraciones y validación están en `AGENTS.md`.
Estado real: 001-022 en `main`; 023 PR #46; 024 PR #47; 025 PR #48; 026 PR #49;
027 PR #50; **028 PR #51 mergeado como `a80c19cc10520f0d2cc6c820c288db9bb28631ab`**.
**029 + 029B CERRADOS** en `feature/calidad-sonar-e2e-029` (PR #52, sin merge):
SonarCloud real y completo — frontend TS + backend C# (SonarScanner for .NET) +
coberturas, Quality Gate verde representativo (deuda #7 RESUELTA, #9 actualizada).
Deuda #10 (Supabase directo) sigue pendiente y fuera de 029.

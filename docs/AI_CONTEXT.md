# SchoolManager - AI Context

## Arquitectura
- Angular frontend, .NET backend y PostgreSQL/Supabase con JWT Supabase.
- Monolito modular, UUID internos, RLS y RPC para escrituras críticas.
- Evitar CQRS, MediatR, microservicios, Generic Repository y UnitOfWork artificial.
- Permisos RBAC, no checks hardcodeados por rol y no DELETE físico de históricos.

## Contexto institucional
- Modo single resuelve la institución activa; modo multi exige contexto explícito.
- El selector global multiinstitución está pendiente.

## Migraciones
- 007 RBAC base; 008 modelo académico histórico; 009 RLS y RPC; 010 identidad; 011 creación alumno con documento; 012 configuración de implementación; 013 centro educativo; 014 ciclos/períodos; 015 períodos anticipados.
- 016 grados, jornadas y secciones: implementada en el repositorio; aplicación en producción no verificada.
- 017 responsables (gestión RPC): implementada y validada en Postgres 16 desechable; baseline consolidado anexado; en `main`.
- 018 configuración financiera (conceptos y planes de pago): implementada y en `main`.
- 019 cargos/mensualidades (obligaciones generadas a partir de planes 018): implementada en `feature/cargos-mensualidades-fase-020` (sin mergear).

## Estándares de ingeniería
- Los **12 principios de ingeniería** del proyecto viven en `docs/engineering-principles.md` y son reglas vinculantes citables por número (`#1`…`#12`) en PRs, handoffs y decisiones técnicas.
- La IA no sustituye la revisión: todo output generado por IA se lee y verifica antes de integrarse (#12).

## Módulo Responsables (018)
- Namespace vigente: **`academico.responsables.*`** (usado por RLS 009 y backend `Permisos.cs`); `responsables.responsables.*` (007) es legacy sin uso, conservado.
- MIG017 cierra la brecha: 9 RPC `security definer` (crear con/para persona, editar, inactivar/reactivar, vincular, editar vinculo, desactivar/reactivar vinculo) con invariantes persona+institución único, principal único por alumno, no-cruce de institución, sin DELETE físico. Superficie solo-RPC (sin policies INSERT/UPDATE directas).
- Backend: `ResponsablesController` (API `api/responsables`, paginado `PaginatedResult`) en `feature/responsables-fase-018`. **Cuidado**: los parámetros nullable a `AddWithValue` deben usar `?? DBNull.Value` (pasar `null` rompe Npgsql en runtime con 500 — los tests de API lo cazan; los de DB no pasan por el controller).
- Frontend: página `/responsables` + servicio `responsables.service.ts` (API .NET) + integración Alumno→Responsable (`responsables?alumnoId=...`, panel de vínculos).

## Módulo Cargos / Mensualidades (020)
- Migración **019** (rama `feature/cargos-mensualidades-fase-020`, sin mergear): tabla `cargos` (obligaciones materializadas desde planes/cuotas de 018, NUNCA sobre las tablas de configuración) + `matriculas.plan_pago_id` + 6 RPC `security definer` (`rpc_listar_cargos_matricula|alumno`, `rpc_resumen_financiero_alumno`, `rpc_asignar_plan_pago_matricula`, `rpc_generar_cargos_matricula` → integer atómico anti-duplicados, `rpc_anular_cargo`).
- Permisos: `academico.cargos.{ver,generar,anular}`.
- **`vencido` es DERIVADO por fecha** (no estado persistido en 020); `pagado`/`parcial` quedan para **021 Pagos**.
- Backend `CargosController` (`api/cargos`): listar por matrícula/alumno, resumen financiero, asignar plan, generar, anular. Institución desde claim `sub`; nullables `?? DBNull.Value`. **Lección**: RPC `RETURNS TABLE` con fila única agregada colapsa a `record` con `select public.rpc_(…)` → usar SIEMPRE `select * from public.rpc_(…)`.
- Frontend: página `/cargos` (lazy, contextual `alumnoId`, acceso desde Alumnos) + `cargos.service.ts` (API .NET); se **retiró** la página legacy `mensualidades` (Supabase `.from('mensualidades')`, tablas inexistentes).

## Módulo Pagos / Cobranza (021)
- Migración **021** (rama `feature/pagos-cobranza-fase-021`): `pagos` (cabecera) + `pagos_aplicaciones`
  (detalle), 1 pago → varios cargos; `alumno_id` obligatorio, `responsable_id` opcional validado en
  contexto, `referencia_externa` opcional única por institución. Superficie solo-RPC (RLS + revoke directo).
- **Saldo SIEMPRE derivado** = `monto_original − SUM(aplicaciones vigentes)`, nunca almacenado;
  `monto_total` = Σ aplicaciones vigentes; sin sobrepago (aplicación ≤ saldo pendiente).
- `cargos.estado` → `pendiente|parcial|pagado|anulado` sincronizado por **triggers en la DB**
  (anulado = explícito; parcial/pagado derivados). Anulación atómica con trazabilidad, sin DELETE físico.
- Permisos `academico.pagos.{ver,registrar,anular}` (iniciales solo admin).
- RPC 019 readaptadas (DROP+CREATE, fix 42P13) anexan `saldo`/`aplicado`/`total_aplicado`; leídas en
  `CargosController` **por índice ordinal**.
- Backend: `PagosController` (`/api/pagos`), `PagoDto`; `CargoDto`/`ResumenFinancieroDto` extendidos.
- Frontend: página `/pagos` (lazy, contextual `alumnoId`, acceso desde Cargos) + `pagos.service.ts`; la
  página `/cargos` muestra saldo derivado y estados parcial/pagado. Detalle en
  `docs/handoffs/021-pagos-cobranza.md`.

## Modelo académico
Institución -> Ciclo -> Período matrícula -> Grado -> Jornada opcional -> Sección -> Matrícula -> Alumno.

- Los períodos pueden ser anticipados, normales o extraordinarios y sus fechas son independientes de las académicas.
- Grados y jornadas son globales por ahora; secciones son por institución/ciclo.
- Una sección con matrículas no cambia ciclo, grado ni jornada.

## Estado frontend
- Módulos navegables: `/configuracion`, `/configuracion/ciclos`, `/configuracion/estructura-academica`, `/configuracion/conceptos-financieros`, `/configuracion/planes-pago`, `/responsables`, `/matriculas`, `/alumnos` y `/cargos` (contextual desde Alumnos).
- Matrículas Fase 1C completa: página `/matriculas` conectada a la API .NET (listar, crear, cambiar estado) con acción "Matricular" desde Alumnos.
- Responsables y Configuración Financiera (conceptos + planes de pago) integrados y en `main`; sus 3 rutas nuevas cargan por lazy (`loadComponent`).
- Detalle de cierre en `docs/handoffs/017D-integracion-alumnos.md`, `017E-tests.md` y `017F-cierre.md`.

## Git y validación
Las reglas operativas de Git, migraciones y validación están en `AGENTS.md`.

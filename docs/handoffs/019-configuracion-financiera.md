# HANDOFF — Bloque 019: Configuración financiera / planes de pago

## Estado: LISTO PARA REVISIÓN — PR abierto (sin mergear por regla)

Rama: `feature/configuracion-financiera-fase-019`.
HEAD: `15cd04a` (pruebas multitenancy/atomicidad).
Base: `main`. No mergear a main. No iniciar 020/021.

## Qué se hizo

- **Migración `018_configuracion_financiera.sql`** (main termina en 016; PR #31
  responsanbles usa 017, por eso este bloque es **018**). Crea `conceptos_financieros`,
  `planes_pago` y `plan_cuotas` con aislamiento por institución (PK/FK UUID,
  montos y vencimientos no negativos, índice único de nombre normalizado por
  institución, orden de cuota único por plan, FK `plan_id` CASCADE /
  `concepto_id` RESTRICT / `instituciones` RESTRICT). Trigger que impide cuotas de
  un plan de otra institución. Inactivación (soft-delete) en vez de DELETE físico,
  coherente con el repo.
- **11 RPC `SECURITY DEFINER`** (patrón `rpc_listar/crear/actualizar/desactivar/reactivar_*`,
  `search_path` seguro, `resolver_institucion_operacion()`, `usuario_tiene_permiso_actual()`,
  `usuario_actual_id()`): 5 para conceptos + 6 para planes. **Crear/actualizar plan es
  atómico** vía JSONB (plan + cuotas en la misma transacción, reemplazo completo de cuotas).
- **Permisos** `configuracion.conceptos_financieros.*` y `configuracion.planes_pago.*`
  (ver/crear/editar/desactivar) asignados al rol `admin`; grants `authenticated` +
  `service_role`; revoke base a `public/anon`.
- **Backend API** (patrón .NET del repo): `ConceptosFinancierosController`
  (`/api/conceptosfinancieros`) y `PlanesPagoController` (`/api/planesPago`). DTOs
  `ConceptoFinancieroDto`, `PlanPagoDto`. Institución resuelta desde contexto
  autenticado (no confía en `institucionId` del cliente en single-instance).
  Nullables con `?? DBNull.Value`. Sin paginación (catálogos pequeños); filtro
  `activo` para listar activos/inactivos.
- **Frontend Angular**: servicio `configuracion-financiera.service.ts` (patrón
  `.NET API` de `matriculas.service.ts`), pantallas
  `configuracion-conceptos-financieros` y `configuracion-planes-pago` (listar,
  crear, editar, activar/desactivar/reactivar, cuotas del plan con reemplazo
  atómico y total, loading, errores, confirmaciones, permisos en componente vía
  `AuthService.tienePermiso()`, sin guards de ruta, sin signals, sin lazy loading
  — convenciones del repo). Acceso desde `/configuracion`.

## Tests

- DB: **83/83** (migración 001→018 aplica limpio; invariantes; RPC).
- API: **44/44** (suite completa) + filtrada `ConfiguracionFinanciera` 11/11.
- Frontend: **93/93** (20 ficheros; incluye 3 specs nuevos de 019).
- Backend build Release: **0 errores**. Frontend build: **0 errores**.
- `git diff --check`: OK.

## Pruebas de multitenancy y atomicidad (revisión humana PR #32)

`ConfiguracionFinancieraMultitenancyTests` (Tests/, fixture mono-institución → el
test crea 2 instituciones y `multiples_instituciones = true`):

1. **Aislamiento institucional** (`AdminB_no_lee_ni_modifica_recursos_de_AdminA`,
   2 inst + 2 admins, `admin` con permisos financieros en su propia inst):
   - AdminB no puede **listar** los recursos de A: `rpc_listar_conceptos_financieros(instA)`
     y `rpc_listar_planes_pago(instA)` → `42501` (su roles solo aplican a instB).
   - AdminB en su propio contexto (instB) no ve recursos de A: listar conceptos → 0 filas.
   - AdminB no puede **editar/desactivar** recursos de A: `rpc_actualizar_concepto`,
     `rpc_desactivar_concepto`, `rpc_obtener_plan` (`P0002` por ownership) y
     `rpc_actualizar_plan` (`P0002`).
   - Después de todo el intento, los recursos de A siguen **intactos** (concepto
     activo visible, nombre del plan sin cambios). Sin fuga cross-tenant vía
     RPC `SECURITY DEFINER` (la seguridad vive en los checks de permiso por
     institución + ownership dentro de la RPC, no en RLS).
2. **Atomicidad de `rpc_actualizar_plan_pago`** (`Auditar_rollback_total`):
   se crea un plan con cuotas [orden 1, monto 100] y [orden 2, monto 200]; se
   intenta reemplazar por cuotas con **orden duplicado** (viola `ux_plan_cuotas_orden`
   → `23505`) **después** del `update` de nombre y del `delete` de cuotas. Al fallar,
   la función (átomica por ser un solo statement) revierte TODO: nombre sigue
   `Plan Original` y cuotas `[100, 200]` (verificado releyendo vía RPC).
3. **Cuota de otra institución** (`Rechaza_plan_con_cuota_de_concepto_de_otra_institucion`):
   el trigger `trg_plan_cuotas_concepto_institucion` rechaza (`23503`) un plan en A
   cuya cuota referencia un concepto de B, revirtiendo la creación (sin filas
   residuales); el mismo plan con concepto de A sí se crea.

Los 3 tests crean sus propios datos (fixture aislado, búsqueda explícita del
estado previo). No cambian la migración ni la fixture existente.

## Fix notable de la migración

`rpc_obtener_plan_pago` tenía colisión de nombres en PL/pgSQL: la out-column `id`
del `RETURNS TABLE` + la variable rowtype `p` + el alias de tabla `p` → `where id
= p_plan_id` ambiguo → PostgresException (400/404). Resuelto: variable renombrada
a `vp`, alias de tabla `p` explícito, columna calificada `p.id`. **Lección**:
en RPC con `RETURNS TABLE` cuyo out param colisiona con nombres internos, califica
siempre el identificador y no reutilices el nombre de la variable rowtype como alias.

## Contrato para 020 (generación de cargos/mensualidades)

La **fuente de configuración** para generar obligaciones futuras es:

- `conceptos_financieros` → catálogo de conceptos (nombre, monto base, descripcion).
- `planes_pago` + `plan_cuotas` → plan y sus cuotas (orden, concepto, monto,
  vencimientoDias); `monto_total` y `total_cuotas` listables en resumen.

020 deberá, al generar mensualidades/cargos por matrícula/alumno, **leer el plan
asignado y materializar sus cuotas** en la capa de obligaciones generadas; no
escribir sobre las tablas de configuración. Se deja documentada la dependencia:
no se mezcla código de Responsables/PR #31 aquí.

## Pendientes reales

- [x] Crear/abrir el **PR contra main** — hecho: **PR #32**.
- [x] Pruebas de multitenancy + atomicidad + rollback (bloque humano PR #32) — **hecho**.
- [x] Checkpoint: commit + push de las pruebas — **hecho**.
- [ ] **Dependencia 016→017 (diferida, NO tocar hasta que PR #31 se mergee)**:
  la migración `018` hoy exige `schema_migrations version='016'`, pero nace desde
  main **sin** la `017` de PR #31 (responsables). Cuando PR #31 esté mergeado en
  main, hacer: `git fetch origin` → integrar `origin/main` con merge normal (sin
  rebase ni force-push) → confirmar `database/migrations/017_*` presente → cambiar
  la precondición de `018_configuracion_financiera.sql` a `version='017'` →
  re-ejecutar la secuencia real 001→018. **No cambiar la numeración de migraciones
  ni hacerlo antes del merge de PR #31.**
- [ ] **Reporte final por Telegram** (formato Fase 9) — pendiente en esta sesión.

## Riesgos / decisiones

- Sin RLS (convención del repo; protección por RPC `SECURITY DEFINER` + checks de
  permiso por institución y ownership — verificado con tests multitenancy).
- Sin paginación server-side en 019 (catálogos pequeños, filtro `activo`); si un
  bloque futuro escala estos listados, aplicar el patrón `PaginatedResult` de PERF-02.
- Baseline consolidado **no** se actualizó (política vigente: la construye el flujo
  de migraciones en orden 001→018).
- La fixture de test es **mono-institución por diseño**; los tests multitenancy
  crean su TI multi-institución sin tocar esa fixture.
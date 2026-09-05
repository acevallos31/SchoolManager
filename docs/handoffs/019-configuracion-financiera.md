# HANDOFF — Bloque 019: Configuración financiera / planes de pago

## Estado: LISTO PARA REVISIÓN — PR abierto (sin mergear por regla)

Rama: `feature/configuracion-financiera-fase-019`.
HEAD: `f50b752`.
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

- DB: **80/80** (migración 001→018 aplica limpio; invariantes; RPC).
- API: **44/44** (suite completa) + filtrada `ConfiguracionFinanciera` 11/11.
- Frontend: **93/93** (20 ficheros; incluye 3 specs nuevos de 019).
- Backend build Release: **0 errores**. Frontend build: **0 errores**.
- `git diff --check`: OK.

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

- Crear/abrir el **PR contra main** y rellenar el HEAD arriba.
- Enviar reporte final por Telegram (formato Fase 9).

## Riesgos / decisiones

- Sin RLS (convención del repo; protección por RPC `SECURITY DEFINER` + checks).
- Sin paginación server-side en 019 (catálogos pequeños, filtro `activo`); si un
  bloque futuro escala estos listados, aplicar el patrón `PaginatedResult` de PERF-02.
- Baseline consolidado **no** se actualizó (política vigente: la construye el flujo
  de migraciones en orden 001→018).
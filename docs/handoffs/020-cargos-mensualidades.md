# HANDOFF — Bloque 020: Cargos / mensualidades / cuentas por cobrar

## Estado: LISTO PARA REVISIÓN — PR abierto (sin mergear por regla)

Rama: `feature/cargos-mensualidades-fase-020`.
Base: `main`. No mergear a main. La fase 021 (pagos) no inicia hasta revisar/mergear.

Commits locales:
- `a291532` `db(020): modelo de cargos y pruebas multitenancy (migracion 019)`
- `05a4d4c` `api(020): controller de cargos y resumen financiero`
- `0d6df17` `fe(020): pagina y servicio de cargos financieros del alumno`
- `fc10e88` `fe(020): retirar flujo mensualidades Supabase y exponer cargos contextuales`

## Qué se hizo

### Migración `019_cargos_mensualidades_obligaciones.sql`
Materializa la **configuración** de 018 (`planes_pago` + `plan_cuotas` +
`conceptos_financieros`) en una capa de **obligaciones generadas** (`cargos`), sin
tocar las tablas de configuración (regla de dominio: la deuda nunca se representa
modificando la configuración). Aislamiento por institución, PK/FK UUID.

- Tabla `cargos`: `institucion_id`, `matricula_id`, `alumno_id`, `plan_pago_id`,
  `orden`, `concepto_id`/`descripcion`/`concepto_nombre` (snapshot), `monto_original > 0`,
  `fecha_vencimiento date` (ancla `ciclos_escolares.fecha_inicio + vencimiento_dias`),
  estado `pendiente|anulado` (soft), `fecha_anulacion`, `motivo_anulacion`,
  `es_vencido` **calculado** (derivado por fecha, no estado persistido).
- `matriculas.plan_pago_id` (asignación de plan a matrícula, única fuente).
- **6 RPC `SECURITY DEFINER`** (patrón `rpc_*`, `search_path` seguro, institución desde
  contexto, ownership verificado): `rpc_listar_cargos_matricula`, `rpc_listar_cargos_alumno`,
  `rpc_resumen_financiero_alumno`, `rpc_asignar_plan_pago_matricula`,
  `rpc_generar_cargos_matricula` (→ integer; atómico, anti-duplicados), `rpc_anular_cargo`.
  Errores: `23505` (duplicado/idempotencia), `22023` (motivo/estado inválidos), `P0002`
  (recurso no existe en el contexto visible del llamador).
- Permisos `academico.cargos.{ver,generar,anular}`.

### Backend API
`CargosController` (`/api/cargos`), patrón .NET del repo, institución resuelta desde el
claim `sub` (no confía en `institucionId` del cliente), nullables con `?? DBNull.Value`:
- `GET  /api/cargos/matricula/{id}`
- `GET  /api/cargos/alumno/{id}`
- `GET  /api/cargos/alumno/{id}/resumen`
- `POST /api/cargos/matricula/{id}/plan` (asignar plan)
- `POST /api/cargos/matricula/{id}/generar`
- `POST /api/cargos/{id}/anular`
DTOs `CargoDto`, `ResumenFinancieroDto`. Nueva clase `Permisos.Cargos`.

**Fix de lectura (no de modelo/contrato):** las RPC `RETURNS TABLE` con fila única
agregada (resumen) colapsan a columna compuesta `record` con `select public.rpc_…(…)`;
se consultan como `select * from public.rpc_…(…)` (aplicado a los 3 listados/resumen
por consistencia). El SQL de las RPC no cambió.

### Frontend Angular
- **Retirado el flujo roto**: página legacy `pages/mensualidades` (consultaba
  `supabase.from('mensualidades')` y tablas `pagos`/`descuentos` inexistentes) y el
  servicio muerto `mensualidad.service.ts`. Quitados los links «Mensualidades» de la
  nav en `app-shell.html` y `dashboard.html` (dashboard repunta al acceso contextual).
- **Nuevo**: servicio `cargos.service.ts` (consumo `environment.apiUrl`, sin
  `institucionId` del cliente), página `pages/cargos` (lectura contextual de `alumnoId`
  desde queryParams, igual que Responsables): resumen de saldo pendiente/vencido/anulado,
  tabla con concepto/monto/vencimiento/estado, badge «Vencido» derivado por fecha,
  estados loading/empty/error, gate por permiso `academico.cargos.ver` vía
  `AuthService.tienePermiso()` (sin guards de ruta, convención del repo).
- **Acceso contextual desde Alumnos**: botón «Cargos» por fila y en el detalle del
  alumno, navega a `/cargos?alumnoId=…` (mismo patrón que Responsables).
- Ruta `/cargos` lazy en `app.routes.ts`.

## Tests
- DB: **108/108** (incluye `CargosMultitenancyTests` 9/9 y secuencia 001→019 aplica limpio).
- API: **66/66** (suite completa). Filtrada `CargosController` 9/9.
- Frontend: **141/141** (23 ficheros; incluye specs nuevos de cargos).
- Backend build Release: **0 errores**. Frontend build production: verificado (log en el
  PR/sesión).
- `git diff --check`: OK.

## Correcciones de tests de lectura (sin tocar el modelo SQL)
Los 3 fallos conocidos de `CargosMultitenancyTests` eran de test/lectura, no defectos de
dominio — corregidos conforme a las directrices del usuario:
1. `min(id)` no existe para `uuid` → leer un cargo concreto (`select id … limit 1`).
2. Npgsql mapea `date` como `DateTime` en esta suite → leer `DateTime` y convertir a
   `DateOnly` (no se cambió el tipo SQL ni el contrato).
3. `AdminB` consultando un recurso de otra institución obtiene `P0002` (no `42501`): la RPC
   valida el recurso primero y no confirma existencia cross-tenant → comportamiento **seguro**
   y **aprobado explícitamente** por el usuario; la expectativa del test se ajustó a `P0002`.

## Contrato para 021 (Pagos)
- **`vencido` NO es un estado persistido en 020**: `es_vencido`/vencido se **deriva por
  fecha** (`fecha_vencimiento < hoy && estado = pendiente`).
- **`pagado`/`parcial` quedan para 021**: en 020 un cargo solo es `pendiente` o `anulado`.
- 021 deberá materializar pagos como entidad propia sobre `cargos` (no sobre
  configuración), manteniendo aislamiento por institución; no mezclar el código de 019
  (planes) ni de 018 en la capa de pagos.
- La asignación de plan → matrícula vive en `matriculas.plan_pago_id` (única fuente).

## Pendientes reales
- [ ] Abrir el **PR contra main** desde `feature/cargos-mensualidades-fase-020` (sin mergear).
- [ ] Reporte final por Telegram (formato Fase 9).
- [ ] Revisión humana del PR (multitenancy en API ya cubierta por tests: AdminB → P0002).

## Riesgos / decisiones
- Sin RLS (convención del repo): seguridad por RPC `SECURITY DEFINER` + checks de permiso
  por institución y ownership (verificado con `CargosMultitenancyTests` + cross-tenant de
  API, que no revela existencia → P0002).
- `generar` es atómico e idempotente/anti-duplicados (transacción completa: todas o ninguna).
- Sin paginación server-side en listados de cargos de un alumno/matrícula (volumen acotado a
  cuotas de un plan); si escala, aplicar `PaginatedResult` de PERF-02.
- Baseline consolidado no se actualizó (política vigente: lo construye la secuencia 001→019).
- No se modificó ninguna tabla de configuración de 018 para representar deuda.

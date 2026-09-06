# HANDOFF — Bloque 021: Pagos / Cobranza / cuentas por cobrar (FASE 2)

## Estado: MERGEADO a main — PR #44 (`f61c931`, 2026-09-06)

Rama: `feature/pagos-cobranza-fase-021`.
Base: `origin/main` = `f61c931` (merge PR #44).
Integrado en `main`; este handoff es el registro histórico del bloque.

## Contrato rector
`docs/decisiones/021-pagos-cobranza-fase1-invariantes.md` (Fase 1, `21794a8`) — INVARIANTES
CERRADAS. Ningún cambio de modelo tras esa aprobación; solo implementación por capas.

## Qué se hizo (FASE 2 completa, por capas)

### 1. Migración `database/migrations/021_pagos_cobranza.sql` (717 líneas) + `validation/021` + `rollback/021`
Modelo transaccional de pagos sobre `cargos` (obligaciones de 019), nunca sobre configuración:
- `pagos` (cabecera) + `pagos_aplicaciones` (detalle): 1 pago → varios cargos;
  `alumno_id` obligatorio, `responsable_id` opcional (validado en contexto),
  `referencia_externa` opcional única por institución, `metodo_pago`, `motivo` de anulación.
- `monto_total` = Σ aplicaciones **vigentes** (check de consistencia en tabla de aplicaciones).
- `cargos.estado` ampliado a `pendiente|parcial|pagado|anulado` y **sincronizado por triggers
  dentro de la DB** (anulado = explícito; parcial/pagado derivados de la suma de aplicaciones).
- **Saldo SIEMPRE derivado**: `saldo = monto_original − SUM(aplicaciones vigentes)`, nunca almacenado.
- RPC 019 readaptadas (DROP+CREATE, fix 42P13) anexando `saldo`/`aplicado` (listado) y
  `total_aplicado` (resumen).
- Nuevas RPC `SECURITY DEFINER` (institución desde contexto; `search_path` seguro;
  cross-tenant/cross-alumno y sobrepago rechazados; anulación atómica con trazabilidad, sin
  DELETE físico): `rpc_registrar_pago`, `rpc_listar_pagos_alumno`, `rpc_obtener_pago`,
  `rpc_obtener_aplicaciones_pago`, `rpc_anular_pago`.
- RLS + `revoke all privileges` sobre `pagos`/`pagos_aplicaciones`; superficie solo-RPC.
- Permisos `academico.pagos.{ver,registrar,anular}` (iniciales solo admin).
- Errores: `23514/23503/23505/P0002/22023/42501`.

### 2. Backend .NET (`SchoolManager.API`)
- `Controllers/PagosController.cs` (197 líneas): `GET /api/pagos/alumno/{alumnoId}`,
  `POST /api/pagos/alumno/{alumnoId}`, `GET /api/pagos/{pagoId}/aplicaciones`,
  `POST /api/pagos/{pagoId}/anular`. Institución desde claim cuando no se envía
  `institucionId` (igual patrón que cargos). Npgsql directo; nullables `?? DBNull.Value`.
- `DTOs/PagoDto.cs` (nuevo); `DTOs/CargoDto.cs` extendido (`Saldo`, `Aplicado`,
  `ResumenFinancieroDto.TotalAplicado`), leídos **por índice ordinal** en
  `CargosController` (`GetDecimal(15)/(16)` listar, `GetDecimal(7)` resumen).
- `Authorization/Permisos.cs`: clase `Pagos`.

### 3. Frontend Angular
- `cargos.service.ts`: `Cargo.estado` ampliado + `saldo`/`aplicado`; `ResumenFinanciero.totalAplicado`.
- `pages/cargos`: estado parcial/pagado/anulado, columna Saldo, card Aplicado, enlace a Pagos.
- `core/services/pagos.service.ts` (nuevo): listar/registrar/aplicaciones/anular (API .NET).
- `pages/pagos` (nuevo, lazy, contextual `alumnoId`): cabecera "Pagos y cobranza", historial de
  pagos (número/monto/fecha/método/referencia/estado), registrar pago seleccionando cargos con
  saldo (validación de no sobrepago: Σ ≤ saldo), anulación con motivo obligatorio.
  Acceso contextual desde la página Cargos. Ruta `/pagos` en `app.routes.ts`.

## Tests
- DB: **145/145** (incluye `PagosMultitenancyTests` **14/14**, commits `4f24bb2` + `c59705e`),
  validation 0 filas, rollback limpio.
- API: **79/79** (suite completa; filtrada `PagosControllerTests` **8/8**, `CargosControllerTests`
  **9/9** sin regresiones).
- Frontend: **162/162** (25 ficheros; incluye specs nuevos de pagos y cargos).
- Builds: backend Release **0 errores**; frontend production **OK** (`dist/`).
- Cobertura FE sobre la suite vitest: Statements 55.6 % (gate no bloqueante en esta fase);
  cobertura backend no re-medida aquí (sin `SONAR_TOKEN` activo).
- `git diff --check`: OK (pendiente re-chequear antes del PR).

## Commits (rama `feature/pagos-cobranza-fase-021`, todos pusheados)
- `4f24bb2` `db(021): agregar modelo transaccional de pagos y aplicaciones`
- `c59705e` `test(021): cubrir invariantes de pagos y saldos (14 casos DB)`
- `f2fc1fc` `api(021): implementar registro y anulacion de pagos con saldo derivado`
- `733f6f1` `test(021): cubrir endpoints de pagos (8 casos API)`
- `2794819` `api(021): exponer saldo y aplicado derivados en cargos y resumen`
- `8f88ad7` `fe(021): agregar flujo de pagos y cobranza (registrar/anular, saldo derivado)`

## Pendientes reales
- [ ] Abrir el **PR contra main** desde `feature/pagos-cobranza-fase-021` (sin mergear).
- [ ] Reporte final por Telegram (formato de cierre).
- [ ] Revisión humana del PR (multitenancy y permisos ya cubiertos por tests).
- [ ] Actualizar `docs/coverage-baseline.json` si el gate de cobertura lo exige.

## Riesgos / decisiones
- Saldo derivado y `cargos.estado` sync viven en la DB (única fuente), no duplicados en API/FE.
- Superficie de pagos solo-RPC + revoke directo: lecturas de persistencia en tests DB se hacen
  por superusuario; las RPC quedan autenticadas.
- Sin paginación server-side en historial de pagos de un alumno (volumen acotado); escalar con
  `PaginatedResult` (PERF-02) si hace falta.
- No se implementó caja avanzada, conciliación bancaria, facturación fiscal, saldo a favor,
  devoluciones complejas ni portal padre como flujo final (fuera de alcance 021).

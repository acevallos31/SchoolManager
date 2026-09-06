# Fase 1 — Invariantes financieras del Bloque 021 (Pagos / Cobranza)

Estado: INVARIANTES CERRADAS (2026-09-06) — aprobado el modelo; pendiente solo
producir la migración (Fase 2). NO escribir código hasta recopilar el texto de Fase 2
o recibir confirmación explícita del mantenedor.

Rama: `feature/pagos-cobranza-fase-021` (desde `main` = `490c5e8`). Sin commits de
código todavía; solo higiene de docs (`2111c05`). Sin merge. No se toca producción
ni Supabase remoto.

## 1. Alcance y linderos (verbatim del mantenedor)

- NO es un módulo contable completo, caja bancaria, facturación fiscal, conciliación
  bancaria ni pasarela de pago.
- NO se adapta `portal-padre` hasta cerrar el modelo de pagos.
- NO se inventa una tabla `mensualidades`: en el modelo actual las obligaciones son
  `public.cargos`.
- NO se duplica el mismo saldo en varias tablas si puede derivarse de forma confiable.
- `vencido` se sigue **derivando por fecha**, no se persiste como estado salvo
  evidencia fuerte.
- Reversión/anulación deja **trazabilidad**: sin DELETE físico de movimientos
  financieros históricos.

## 2. Modelo base actual (migración 019, verificado en DDL)

`public.cargos` (obligación generada por matrícula):
- `monto_original numeric(12,2) > 0` — el cargo es por su monto total original.
- `estado text in ('pendiente','anulado')` con `default 'pendiente'`.
- `fecha_vencimiento date`; `fecha_anulacion`/`motivo_anulacion` coherentes con
  `estado='anulado'` (`ck_cargos_anulacion_coherente`).
- `es_vencido` derivado en RPC: `estado='pendiente' and fecha_vencimiento < current_date`.
- RLS activa + sin policies; superficie **RPC-only** (`security definer`), permisos
  `academico.cargos.{ver,generar,anular}`.

Hallazgo conductor de la Fase 0:
- `rpc_resumen_financiero_alumno` calcula `total_pendiente = SUM(monto_original)
  WHERE estado='pendiente'` → hoy **un cargo pagado a medias no es representable**
  (no existe pago aplicado que reduzca el saldo).

## 3. Modelo aprobado 021 (núcleo)

Tres conceptos, siguiendo el patrón RPC-only + checksum + validation del repo:

- `public.cargos` — se **amplía** semánticamente (no se reescribe el CHECK actual de
  forma destructiva): `estado` pasa a `('pendiente','parcial','pagado','anulado')`.
  El saldo **NO se almacena**; se deriva siempre. `anulado` es transición explícita
  e independiente.
- `public.pagos` — **cabecera / recibo** de la transacción:
  `institucion_id`, `alumno_id` (obligatorio), `responsable_id` (opcional, pagador si
  corresponde a un responsable registrado), `monto_total`, `fecha_pago`, `metodo_pago`,
  `referencia_externa` (opcional, única), estado (`registrado`,`anulado`), timestamps +
  trazabilidad de quién/anulación.
- `public.pagos_aplicaciones` — **detalle** que distribuye el monto del pago entre
  cargos: `pago_id`, `cargo_id`, `monto_aplicado`, estado de la aplicación
  (`vigente`,`reversada`), timestamps. Un cargo puede recibir múltiples aplicaciones a
  lo largo del tiempo. Saldo pendiente de un cargo = `monto_original − SUM(monto_aplicado
  de aplicaciones vigentes)`.

Saldos resumidos (por alumno / por cargo) **siempre derivados** por agregación; nunca
materializados.

## 4. Invariantes financieras (confirmadas)

> La primera es el anchor literal de la orden. El resto fueron aprobadas/resueltas por
> el mantenedor en el cierre de Fase 1. Se imponen en DB: CHECKs donde sean de fila +
> guardas dentro de RPC `security definer` atómica + (si hace falta) trigger para
> mantener `cargos.estado` derivado de las aplicaciones.

### Por pago
- **I1. Positivity:** `monto_total > 0` y cada `monto_aplicado > 0`.
- **I2. La suma de aplicaciones de un pago debe ser IGUAL a su `monto_total`** (salvo
  que se diseñe explícitamente saldo a favor más adelante) — por tanto nunca la supera
  (anchor «≤» se cumple; se exige `=` salvo decisión futura explícita).
- **I3. Cargo destino del mismo contexto:** toda aplicación apunta a un cargo cuya
  `institucion_id` = la del pago = la del `alumno_id` del pago.
- **I4. No sobre-pagar un cargo:** `SUM(monto_aplicado vigente) ≤ monto_original`
  (nunca saldo < 0). Si una aplicación completa el saldo → cargo `pagado`; si queda
  saldo → `parcial`.

### Por cargo
- **I5. No aplicar a un cargo `anulado`.**
- **I6. Fecha coherente:** `fecha_pago ≥ fecha_generacion` del cargo aplicado.
- **I7. Sin pagos a cuenta:** un pago siempre se aplica a uno o más cargos al
  registrarse (no existe «pago sin cargo destino»). Un cargo puede recibir múltiples
  aplicaciones; pago parcial = aplicar menos que el saldo del cargo.

### Trazabilidad
- **I8. Anular pago = reversión con trazabilidad:** `pagos.estado='anulado'`; sus
  aplicaciones pasan a `reversada` (nunca DELETE físico). El cargo vuelve a reflejar el
  saldo pendiente según las aplicaciones vigentes restantes.

## 5. Decisiones resueltas (2026-09-06, mantenedor)

1. **`cargos.estado` y saldo (opción 1 aprobada).** Saldo derivado de
   `monto_original − SUM(pagos aplicados válidos)`, nunca almacenado como fuente de
   verdad. `cargos.estado` se amplía a `('pendiente','parcial','pagado','anulado')`.
   `parcial`/`pagado` se mantienen **automáticamente desde DB** según los pagos
   aplicados, dentro de la **misma RPC atómica** de aplicación/reversión (nunca por
   cambios arbitrarios del frontend). `anulado` = transición explícita independiente.
   **Nunca saldo < 0**; sin sobrepago salvo diseño explícito futuro.

2. **Aplicación a uno o varios cargos (opción 1 aprobada).** `pago` = cabecera/recibo;
   `pagos_aplicaciones` (o equivalente) = detalle que distribuye el monto entre cargos;
   cada aplicación registra cuánto se aplicó a un cargo; un cargo recibe múltiples
   aplicaciones en el tiempo; suma aplicada a un cargo ≤ su saldo pendiente; suma de
   aplicaciones de un pago = `monto_total` del pago (salvo saldo a favor futuro
   explícito); pago parcial = aplicar menos que el saldo; si una aplicación completa el
   saldo el cargo pasa a `pagado`, si queda → `parcial`.

3. **Pagador (aprobada).** Todo pago queda asociado al **alumno** (`alumno_id`
   obligatorio). Si el pagador corresponde a un **responsable registrado**, se guarda
   `responsable_id` (opcional). NO se exige responsable para un pago válido. Se valida
   que el responsable, cuando se informe, pertenezca al mismo contexto institucional y
   tenga relación válida con el alumno según el modelo actual (018). NO inferir
   automáticamente que el responsable principal fue quien pagó. NO crear responsables
   ficticios solo para registrar un pago.

4. **Anti-duplicado de recibo (opción 1 aprobada).** `referencia_externa` única
   **OPCIONAL** (número de transferencia/cheque), con control de duplicados **si se
   provee**; control interno propio (recibo correlativo auto-generado) siempre.

5. **Permisos (opción 1 aprobada).** Permisos propios: `academico.pagos.ver`,
   `academico.pagos.registrar`, `academico.pagos.anular`. Asignados **solo a `admin`**
   por ahora, siguiendo el patrón del repo. RPC `SECURITY DEFINER` validan estos
   permisos **en DB**; backend .NET y frontend usan el mismo namespace
   `academico.pagos.*`, sin hardcodear por rol. NO reutilizar `academico.cargos.*`.

## 6. Forma de cierre

Con las invariantes cerradas, producir (Fase 2): `021_pagos_cobranza.sql` +
`validation/021_….validation.sql` (negativa → 0 filas) + `rollback/021_….rollback.sql`
+ auto-registro en `schema_migrations`, replicando el patrón checksum/validación de 020.

> Nota de trazabilidad: el texto íntegro de la **FASE 2** de la orden (formato exacto de
> la migración y sus fases) quedó truncado en el historial y NO se recuperó; si el
> mantenedor tiene el texto original, aportarlo **antes de escribir la migración** para
> no desviarse de la especificación. La Fase 1 (este documento) ya está cerrada y no
> depende de ese tramo.

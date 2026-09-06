-- Migracion 021: Pagos / Cobranza (modelo transaccional).
--
-- Cierra la deuda #6 (docs/technical-debt.md) y materializa el contrato
-- docs/decisiones/021-pagos-cobranza-fase1-invariantes.md (INVARIANTES CERRADAS).
--
-- Alcance (verbatim del mantenedor): NO es modulo contable completo, caja
-- bancaria, facturacion fiscal, conciliacion bancaria ni pasarela de pago. Las
-- obligaciones siguen siendo public.cargos (no se crea tabla mensualidades).
--
-- Que agrega:
--   1. public.pagos            -- cabecera / recibo de la transaccion.
--   2. public.pagos_aplicaciones -- detalle que reparte el monto entre cargos.
--   3. Ampliacion de cargos.estado a ('pendiente','parcial','pagado','anulado').
--      El saldo NO se almacena; se deriva (monto_original - SUM aplicaciones
--      vigentes). cargos.estado se mantiene automaticamente desde DB dentro de
--      las RPC atomicas (nunca por cambios arbitrarios del cliente). 'anulado'
--      es transicion explicita e independiente.
--   4. RPC de registro, listado, detalle y anulacion de pagos.
--   5. Permisos propios academico.pagos.{ver,registrar,anular} (solo admin).
--
-- Reversion con trazabilidad: anular un pago = pagos.estado='anulado' y sus
-- aplicaciones vigentes pasan a 'reversada' (NUNCA DELETE fisico). Sin overpay
-- ni saldo negativo. Superficie RPC-only (sin policies), igual que cargos 019.

begin;

-- ======================================================================
-- 1. AMPLIACION SEGURA DE cargos.estado
-- ======================================================================
-- El CHECK inline creado en 019 (estado in ('pendiente','anulado')) no tiene
-- nombre propio; se localiza por el atributo (columna estado) y se sustituye por
-- uno con los cuatro estados. Se preserva ck_cargos_anulacion_coherente (toca
-- estado + fecha/motivo anulacion, no es el check de dominio de estado).
do $$
declare v_check text;
begin
  select c.conname into v_check
  from pg_constraint c
  join pg_class t on t.oid = c.conrelid and t.relname = 'cargos'
  join pg_attribute a on a.attrelid = c.conrelid
     and a.attnum = any (c.conkey) and a.attname = 'estado'
  where c.contype = 'c'
    and c.conname <> 'ck_cargos_anulacion_coherente';
  if v_check is not null then
    execute format('alter table public.cargos drop constraint %I', v_check);
  end if;
end $$;

alter table public.cargos
  add constraint ck_cargos_estado check (estado in ('pendiente','parcial','pagado','anulado'));

-- Indice existente ix_cargos_alumno_estado (institucion_id, alumno_id, estado,
-- fecha_vencimiento) sigue siendo util para listar pendientes/parciales.

-- ======================================================================
-- 2. SECUENCIA INTERNA DE RECIBO (control propio, auto-generado)
-- ======================================================================
-- Numero de recibo correlativo de control interno. Global (no por institucion)
-- porque es un identificador de control interno; la unicidad de negocio opcional
-- (transferencia/cheque) se cubre con referencia_externa por institucion.
create sequence if not exists public.seq_pagos_numero_recibo;

-- ======================================================================
-- 3. TABLA public.pagos (cabecera / recibo)
-- ======================================================================
create table if not exists public.pagos (
  id uuid primary key default gen_random_uuid(),
  institucion_id uuid not null references public.instituciones(id) on delete restrict,
  alumno_id uuid not null,
  responsable_id uuid null references public.responsables(id) on delete restrict,
  numero_recibo bigint not null default nextval('public.seq_pagos_numero_recibo'),
  monto_total numeric(12,2) not null check (monto_total > 0),
  fecha_pago timestamptz not null default now(),
  metodo_pago text null,
  referencia_externa text null,
  estado text not null default 'registrado'
    check (estado in ('registrado','anulado')),
  registrado_por uuid not null references public.usuarios(id),
  fecha_anulacion timestamptz null,
  anulado_por uuid null references public.usuarios(id),
  motivo_anulacion text null,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint fk_pagos_alumno_institucion foreign key (alumno_id, institucion_id)
    references public.alumnos(id, institucion_id) on delete restrict,
  constraint ck_pagos_anulacion_coherente check (
    (estado = 'anulado' and fecha_anulacion is not null
      and anulado_por is not null and motivo_anulacion is not null
      and btrim(motivo_anulacion) <> '')
    or (estado = 'registrado' and fecha_anulacion is null
      and anulado_por is null and motivo_anulacion is null)
  ),
  constraint uq_pagos_numero_recibo unique (numero_recibo)
);

-- Unicidad razonable de referencia externa POR INSTITUCION cuando se provee.
create unique index if not exists ux_pagos_referencia_externa_institucion
  on public.pagos (institucion_id, referencia_externa)
  where referencia_externa is not null;

-- Consultas tipicas: listado por alumno y control de contexto institucional.
create index if not exists ix_pagos_alumno_fecha
  on public.pagos (institucion_id, alumno_id, fecha_pago desc);

-- ======================================================================
-- 4. TABLA public.pagos_aplicaciones (detalle / distribucion entre cargos)
-- ======================================================================
create table if not exists public.pagos_aplicaciones (
  id uuid primary key default gen_random_uuid(),
  pago_id uuid not null references public.pagos(id) on delete restrict,
  cargo_id uuid not null references public.cargos(id) on delete restrict,
  institucion_id uuid not null references public.instituciones(id) on delete restrict,
  monto_aplicado numeric(12,2) not null check (monto_aplicado > 0),
  estado text not null default 'vigente'
    check (estado in ('vigente','reversada')),
  fecha_reversion timestamptz null,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint uq_pagos_aplicaciones_pago_cargo unique (pago_id, cargo_id)
);

create index if not exists ix_pagos_aplicaciones_cargo
  on public.pagos_aplicaciones (cargo_id, estado);
create index if not exists ix_pagos_aplicaciones_pago
  on public.pagos_aplicaciones (pago_id, institucion_id);

-- Integridad de contexto y saldo por aplicacion: el cargo destino debe
-- pertenecer a la misma institucion y al mismo alumno del pago; no puede
-- aplicarse a un cargo anulado; y la aplicacion no puede exceder el saldo
-- pendiente del cargo (I3, I4, I5 del contrato). Trigger BEFORE de guarda
-- (defensa en DB para cualquier camino de escritura).
create or replace function public.trg_pagos_aplicaciones_guard() returns trigger
language plpgsql set search_path = pg_catalog, public, pg_temp as $$
declare v_cargo public.cargos%rowtype; v_pago public.pagos%rowtype;
        v_aplicado numeric(12,2); v_saldo numeric(12,2);
begin
  select * into v_pago from public.pagos where id = new.pago_id for update;
  if not found then
    raise exception 'El pago no existe.' using errcode = '23503';
  end if;
  select * into v_cargo from public.cargos where id = new.cargo_id for update;
  if not found then
    raise exception 'El cargo no existe.' using errcode = '23503';
  end if;
  if v_cargo.institucion_id is distinct from v_pago.institucion_id
     or new.institucion_id is distinct from v_pago.institucion_id then
    raise exception 'El cargo no pertenece a la institucion del pago.' using errcode = '23503';
  end if;
  if v_cargo.alumno_id is distinct from v_pago.alumno_id then
    raise exception 'El cargo no pertenece al alumno del pago.' using errcode = '23503';
  end if;
  if v_cargo.estado = 'anulado' then
    raise exception 'No se puede aplicar un pago a un cargo anulado.' using errcode = '23503';
  end if;
  if new.estado = 'vigente' then
    -- Solo las aplicaciones vigentes consumen saldo (las reversadas lo liberan).
    select coalesce(sum(monto_aplicado), 0) into v_aplicado
    from public.pagos_aplicaciones
    where cargo_id = new.cargo_id and estado = 'vigente'
      and id is distinct from coalesce(new.id, null);
    v_saldo := v_cargo.monto_original - v_aplicado;
    if new.monto_aplicado > v_saldo then
      raise exception 'La aplicacion supera el saldo pendiente del cargo (sobrepago).' using errcode = '23514';
    end if;
  end if;
  return new;
end $$;

create trigger trg_pagos_aplicaciones_guard_before
  before insert or update of pago_id, cargo_id, institucion_id, monto_aplicado, estado
  on public.pagos_aplicaciones for each row execute function public.trg_pagos_aplicaciones_guard();

-- No anular un cargo que ya recibio aplicaciones vigentes (el dinero recaudado
-- debe revertirse primero anulando el/los pagos). Guarda sobre cargos.
create or replace function public.trg_cargos_no_anular_con_pagos() returns trigger
language plpgsql set search_path = pg_catalog, public, pg_temp as $$
begin
  if new.estado = 'anulado' and exists (
      select 1 from public.pagos_aplicaciones pa
      join public.pagos pg on pg.id = pa.pago_id
      where pa.cargo_id = new.id and pa.estado = 'vigente' and pg.estado = 'registrado') then
    raise exception 'No se puede anular un cargo con aplicaciones vigentes; anule primero el pago.' using errcode = '23503';
  end if;
  return new;
end $$;

create trigger trg_cargos_no_anular_con_pagos_before
  before update of estado on public.cargos for each row execute function public.trg_cargos_no_anular_con_pagos();

-- Mantener cargos.estado siempre consistente con las aplicaciones, cualquier
-- sea la via de escritura: tras insertar/actualizar/reversar una aplicacion se
-- recalcula el estado del cargo afectado (idempotente, no toca 'anulado').
create or replace function public.trg_pagos_aplicaciones_sync_estado() returns trigger
language plpgsql set search_path = pg_catalog, public, pg_temp as $$
declare v_cargo uuid;
begin
  v_cargo := coalesce(new.cargo_id, old.cargo_id);
  if v_cargo is not null then
    perform public.recalcular_estado_cargo(v_cargo);
  end if;
  return null;
end $$;

create trigger trg_pagos_aplicaciones_sync_estado_after
  after insert or update or delete on public.pagos_aplicaciones
  for each row execute function public.trg_pagos_aplicaciones_sync_estado();

-- ======================================================================
-- 5. Superficie RPC-only para las tablas nuevas (sin policies), como cargos 019.
-- ======================================================================
alter table public.pagos enable row level security;
alter table public.pagos_aplicaciones enable row level security;
revoke all privileges on table public.pagos, public.pagos_aplicaciones
  from public, anon, authenticated;
grant all privileges on table public.pagos, public.pagos_aplicaciones
  to postgres, service_role;

revoke all privileges on sequence public.seq_pagos_numero_recibo from public, anon;
grant usage, select on sequence public.seq_pagos_numero_recibo to authenticated;
grant all privileges on sequence public.seq_pagos_numero_recibo to service_role;

-- ======================================================================
-- 6. PERMISOS propios (solo admin por ahora)
-- ======================================================================
insert into public.permisos(codigo, modulo, nombre) values
  ('academico.pagos.ver',       'academico', 'Ver pagos y aplicaciones'),
  ('academico.pagos.registrar', 'academico', 'Registrar pagos'),
  ('academico.pagos.anular',    'academico', 'Anular pagos')
on conflict (codigo) do nothing;

insert into public.roles_permisos(rol_id, permiso_id)
select r.id, p.id
from public.roles r cross join public.permisos p
where r.codigo = 'admin' and p.codigo like 'academico.pagos.%'
on conflict do nothing;

-- ======================================================================
-- 7. Helper interno: recalcular estado de un cargo desde sus aplicaciones.
-- ======================================================================
create or replace function public.recalcular_estado_cargo(p_cargo_id uuid)
returns void language plpgsql security definer
set search_path = pg_catalog, public, pg_temp as $$
declare v_original numeric(12,2); v_aplicado numeric(12,2); v_estado text;
begin
  select monto_original, estado into v_original, v_estado
  from public.cargos where id = p_cargo_id;
  if not found or v_estado = 'anulado' then
    -- anulado es transicion explicita: no se toca.
    return;
  end if;
  select coalesce(sum(pa.monto_aplicado), 0) into v_aplicado
  from public.pagos_aplicaciones pa
  join public.pagos pg on pg.id = pa.pago_id
  where pa.cargo_id = p_cargo_id and pa.estado = 'vigente' and pg.estado = 'registrado';
  if v_aplicado <= 0 then
    v_estado := 'pendiente';
  elsif v_aplicado >= v_original then
    v_estado := 'pagado';
  else
    v_estado := 'parcial';
  end if;
  update public.cargos set estado = v_estado, updated_at = now()
  where id = p_cargo_id and estado <> 'anulado';
end $$;

-- ======================================================================
-- 8. RPC: REGISTRAR PAGO + APLICACIONES (atomico)
-- ======================================================================
create or replace function public.rpc_registrar_pago(
  p_alumno_id uuid, p_aplicaciones jsonb, p_monto_total numeric,
  p_institucion_id uuid default null,
  p_responsable_id uuid default null, p_metodo_pago text default null,
  p_referencia_externa text default null, p_fecha_pago timestamptz default null)
returns uuid language plpgsql security definer
set search_path = pg_catalog, public, pg_temp as $$
declare v_institucion uuid; v_usuario uuid; v_pago_id uuid;
        v_suma numeric(12,2) := 0; v_fecha timestamptz;
        c jsonb; v_cargo uuid; v_monto numeric(12,2);
        cr public.cargos%rowtype; v_cargos_aplicados integer := 0;
begin
  v_usuario := public.usuario_actual_id();
  if v_usuario is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v_institucion := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.pagos.registrar', v_institucion) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  -- Alumno debe existir en la institucion resuelta del contexto.
  if not exists (select 1 from public.alumnos a
                 where a.id = p_alumno_id and a.institucion_id = v_institucion) then
    raise exception 'El alumno no existe.' using errcode = 'P0002';
  end if;

  -- Responsable opcional: debe ser valido en el mismo contexto institucional y
  -- estar vinculado al alumno segun el modelo actual (responsables/alumno_responsable).
  if p_responsable_id is not null then
    if not exists (
      select 1 from public.responsables r
      where r.id = p_responsable_id and r.institucion_id = v_institucion
        and r.estado = 'activo'
        and exists (select 1 from public.alumno_responsable ar
                    where ar.responsable_id = r.id and ar.alumno_id = p_alumno_id
                      and ar.estado = 'activo')) then
      raise exception 'El responsable no es valido para este alumno en la institucion.' using errcode = '23503';
    end if;
  end if;

  -- Aplicaciones como arreglo JSON (patron de rpc_crear_plan_pago 018).
  if p_aplicaciones is null or jsonb_typeof(p_aplicaciones) <> 'array' then
    raise exception 'Las aplicaciones deben enviarse como un arreglo JSON.' using errcode = '22023';
  end if;
  if jsonb_array_length(p_aplicaciones) = 0 then
    raise exception 'El pago debe aplicarse a al menos un cargo.' using errcode = '22023';
  end if;

  -- Invariante I1/I2: monto_total > 0 y la suma de aplicaciones DEBE igualar el
  -- monto_total declarado (rechaza cualquier desajuste antes de escribir).
  if p_monto_total is null or p_monto_total <= 0 then
    raise exception 'El monto total del pago debe ser mayor a cero.' using errcode = '22023';
  end if;

  v_fecha := coalesce(p_fecha_pago, now());

  -- Pasada 1: validar cargos, contexto, estado, saldo y acumular la suma.
  -- Bloquea cada cargo (for update) para impedir sobrepago concurrente.
  for c in select * from jsonb_array_elements(p_aplicaciones)
  loop
    if jsonb_typeof(c) <> 'object'
       or (c->>'cargo_id') is null or (c->>'monto') is null
       or (c->>'monto')::numeric <= 0 then
      raise exception 'Los datos de una aplicacion no son validos.' using errcode = '22023';
    end if;
    v_cargo := (c->>'cargo_id')::uuid;
    v_monto := (c->>'monto')::numeric;
    select * into cr from public.cargos
    where id = v_cargo and institucion_id = v_institucion
      and alumno_id = p_alumno_id for update;
    if not found then
      raise exception 'El cargo no existe o no pertenece al alumno en la institucion.' using errcode = 'P0002';
    end if;
    if cr.estado = 'anulado' then
      raise exception 'No se puede aplicar un pago a un cargo anulado.' using errcode = '23503';
    end if;
    if v_fecha < cr.fecha_generacion then
      raise exception 'La fecha del pago no puede ser anterior a la generacion del cargo.' using errcode = '22023';
    end if;
    v_suma := v_suma + v_monto;
  end loop;

  if v_suma <> p_monto_total then
    raise exception 'La suma de las aplicaciones no coincide con el monto total del pago.' using errcode = '23514';
  end if;

  insert into public.pagos(institucion_id, alumno_id, responsable_id, monto_total,
      fecha_pago, metodo_pago, referencia_externa, estado, registrado_por)
  values (v_institucion, p_alumno_id, p_responsable_id, v_suma,
      v_fecha, nullif(btrim(coalesce(p_metodo_pago,'')),''),
      nullif(btrim(coalesce(p_referencia_externa,'')),''), 'registrado', v_usuario)
  returning id into v_pago_id;

  -- Pasada 2: materializar aplicaciones (cada insert dispara la guarda y el
  -- sync de estado) y recalcular el estado de cada cargo aplicado.
  for c in select * from jsonb_array_elements(p_aplicaciones)
  loop
    v_cargo := (c->>'cargo_id')::uuid;
    v_monto := (c->>'monto')::numeric;
    insert into public.pagos_aplicaciones(pago_id, cargo_id, institucion_id, monto_aplicado, estado)
    values (v_pago_id, v_cargo, v_institucion, v_monto, 'vigente');
    perform public.recalcular_estado_cargo(v_cargo);
    v_cargos_aplicados := v_cargos_aplicados + 1;
  end loop;

  return v_pago_id;
end $$;

-- ======================================================================
-- 9. RPC: LISTAR PAGOS POR ALUMNO
-- ======================================================================
create or replace function public.rpc_listar_pagos_alumno(
  p_alumno_id uuid, p_institucion_id uuid default null)
returns table(id uuid, institucion_id uuid, alumno_id uuid, responsable_id uuid,
              numero_recibo bigint, monto_total numeric, fecha_pago timestamptz,
              metodo_pago text, referencia_externa text, estado text,
              registrado_por uuid, fecha_anulacion timestamptz, anulado_por uuid,
              motivo_anulacion text, created_at timestamptz)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.pagos.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if not exists (select 1 from public.alumnos a
                 where a.id = p_alumno_id and a.institucion_id = v) then
    raise exception 'El alumno no existe.' using errcode = 'P0002';
  end if;
  return query
    select pg.id, pg.institucion_id, pg.alumno_id, pg.responsable_id,
           pg.numero_recibo, pg.monto_total, pg.fecha_pago,
           pg.metodo_pago, pg.referencia_externa, pg.estado,
           pg.registrado_por, pg.fecha_anulacion, pg.anulado_por,
           pg.motivo_anulacion, pg.created_at
    from public.pagos pg
    where pg.alumno_id = p_alumno_id and pg.institucion_id = v
    order by pg.fecha_pago desc, pg.created_at desc, pg.id;
end $$;

-- ======================================================================
-- 10. RPC: DETALLE DE PAGO (cabecera) y APLICACIONES del pago
-- ======================================================================
create or replace function public.rpc_obtener_pago(
  p_pago_id uuid, p_institucion_id uuid default null)
returns table(id uuid, institucion_id uuid, alumno_id uuid, responsable_id uuid,
              numero_recibo bigint, monto_total numeric, fecha_pago timestamptz,
              metodo_pago text, referencia_externa text, estado text,
              registrado_por uuid, fecha_anulacion timestamptz, anulado_por uuid,
              motivo_anulacion text, created_at timestamptz)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.pagos.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  return query
    select pg.id, pg.institucion_id, pg.alumno_id, pg.responsable_id,
           pg.numero_recibo, pg.monto_total, pg.fecha_pago,
           pg.metodo_pago, pg.referencia_externa, pg.estado,
           pg.registrado_por, pg.fecha_anulacion, pg.anulado_por,
           pg.motivo_anulacion, pg.created_at
    from public.pagos pg
    where pg.id = p_pago_id and pg.institucion_id = v;
end $$;

create or replace function public.rpc_obtener_aplicaciones_pago(
  p_pago_id uuid, p_institucion_id uuid default null)
returns table(aplicacion_id uuid, pago_id uuid, cargo_id uuid, institucion_id uuid,
              monto_aplicado numeric, estado text, fecha_reversion timestamptz,
              cargo_estado text, concepto_nombre text, monto_original numeric)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.pagos.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if not exists (select 1 from public.pagos pg
                 where pg.id = p_pago_id and pg.institucion_id = v) then
    raise exception 'El pago no existe.' using errcode = 'P0002';
  end if;
  return query
    select pa.id, pa.pago_id, pa.cargo_id, pa.institucion_id,
           pa.monto_aplicado, pa.estado, pa.fecha_reversion,
           c.estado, c.concepto_nombre, c.monto_original
    from public.pagos_aplicaciones pa
    join public.cargos c on c.id = pa.cargo_id
    where pa.pago_id = p_pago_id and pa.institucion_id = v
    order by pa.created_at, pa.id;
end $$;

-- ======================================================================
-- 11. RPC: ANULAR / REVERTIR PAGO (atomico, con trazabilidad)
-- ======================================================================
create or replace function public.rpc_anular_pago(
  p_pago_id uuid, p_motivo text, p_institucion_id uuid default null)
returns void language plpgsql security definer
set search_path = pg_catalog, public, pg_temp as $$
declare v_institucion uuid; v_usuario uuid;
        v_pago public.pagos%rowtype; v_cargo uuid;
begin
  v_usuario := public.usuario_actual_id();
  if v_usuario is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v_institucion := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.pagos.anular', v_institucion) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if btrim(coalesce(p_motivo, '')) = '' then
    raise exception 'El motivo de anulacion es obligatorio.' using errcode = '22023';
  end if;
  select * into v_pago from public.pagos
  where id = p_pago_id and institucion_id = v_institucion for update;
  if not found then
    raise exception 'El pago no existe.' using errcode = 'P0002';
  end if;
  if v_pago.estado = 'anulado' then
    raise exception 'El pago ya esta anulado.' using errcode = '22023';
  end if;

  -- 1) Revertir aplicaciones vigentes del pago (sin DELETE fisico).
  update public.pagos_aplicaciones
  set estado = 'reversada', fecha_reversion = now(), updated_at = now()
  where pago_id = v_pago.id and estado = 'vigente';

  -- 2) Marcar el pago como anulado (trazabilidad).
  update public.pagos
  set estado = 'anulado', fecha_anulacion = now(), anulado_por = v_usuario,
      motivo_anulacion = btrim(p_motivo), updated_at = now()
  where id = v_pago.id;

  -- 3) Recalcular estados de los cargos que recibieron ese pago.
  for v_cargo in
    select distinct cargo_id from public.pagos_aplicaciones where pago_id = v_pago.id
  loop
    perform public.recalcular_estado_cargo(v_cargo);
  end loop;
end $$;

-- ======================================================================
-- 12. ADAPTAR RPC DE CARGOS 019 AL SALDO DERIVADO
-- ======================================================================
-- Con la ampliacion de cargos.estado a ('pendiente','parcial','pagado',
-- 'anulado'), los resumenes 019 que sumaban monto_original de los 'pendiente'
-- dejarian de reflejar pagos parciales. Se redefinen saldo-aware: el saldo
-- pendiente de un cargo = monto_original - SUM(aplicaciones vigentes), y el
-- estado derivado decide la clasificacion. 'anulado' es explicito e independiente.
-- Se preservan las primeras columnas que consume CargosController (leyendo por
-- indice) y se ANEXAN las nuevas al final; las RPC quedan como superficie real.
--
-- NOTA de conveniencia: rpc_listar_cargos_* y rpc_resumen_financiero_alumno
-- fueron creadas en 019 con GRANT a authenticated/service_role; CREATE OR
-- REPLACE conserva los privilegios de la funcion, por lo que no hace falta
-- regrantearlas aqui (se reitera igualmente por legibilidad al final del
-- CargosController ya lee las primeras 15 columnas de listar y 7 de
-- resumen por indice, de modo que anexar saldo NO rompe el backend.

-- Se eliminan primero las funciones 019 (anadir columnas de retorno no se puede
-- hacer con CREATE OR REPLACE: 42P13) y se recrean saldo-aware.
drop function if exists public.rpc_listar_cargos_matricula(uuid, uuid);
create or replace function public.rpc_listar_cargos_matricula(
  p_matricula_id uuid, p_institucion_id uuid default null)
returns table(id uuid, matricula_id uuid, alumno_id uuid, plan_pago_id uuid, orden integer,
              concepto_id uuid, concepto_nombre text, descripcion text, monto_original numeric,
              fecha_vencimiento date, estado text, fecha_generacion timestamptz,
              fecha_anulacion timestamptz, motivo_anulacion text, es_vencido boolean,
              saldo numeric, aplicado numeric)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.cargos.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if not exists(select 1 from public.matriculas m
                where m.id = p_matricula_id and m.institucion_id = v) then
    raise exception 'La matricula no existe.' using errcode = 'P0002';
  end if;
  return query
    select c.id, c.matricula_id, c.alumno_id, c.plan_pago_id, c.orden,
           c.concepto_id, c.concepto_nombre, c.descripcion, c.monto_original,
           c.fecha_vencimiento, c.estado, c.fecha_generacion, c.fecha_anulacion,
           c.motivo_anulacion,
           (c.estado in ('pendiente','parcial')
             and c.monto_original - coalesce(ap.aplicado, 0) > 0
             and c.fecha_vencimiento < current_date) as es_vencido,
           round(c.monto_original - coalesce(ap.aplicado, 0), 2) as saldo,
           coalesce(ap.aplicado, 0) as aplicado
    from public.cargos c
    left join lateral (
      select sum(pa.monto_aplicado) as aplicado
      from public.pagos_aplicaciones pa
      join public.pagos pg on pg.id = pa.pago_id
      where pa.cargo_id = c.id and pa.estado = 'vigente' and pg.estado = 'registrado'
    ) ap on true
    where c.matricula_id = p_matricula_id and c.institucion_id = v
    order by c.orden, c.fecha_vencimiento, c.id;
end $$;

drop function if exists public.rpc_listar_cargos_alumno(uuid, uuid);
create or replace function public.rpc_listar_cargos_alumno(
  p_alumno_id uuid, p_institucion_id uuid default null)
returns table(id uuid, matricula_id uuid, alumno_id uuid, plan_pago_id uuid, orden integer,
              concepto_id uuid, concepto_nombre text, descripcion text, monto_original numeric,
              fecha_vencimiento date, estado text, fecha_generacion timestamptz,
              fecha_anulacion timestamptz, motivo_anulacion text, es_vencido boolean,
              saldo numeric, aplicado numeric)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.cargos.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if not exists(select 1 from public.alumnos a
                where a.id = p_alumno_id and a.institucion_id = v) then
    raise exception 'El alumno no existe.' using errcode = 'P0002';
  end if;
  return query
    select c.id, c.matricula_id, c.alumno_id, c.plan_pago_id, c.orden,
           c.concepto_id, c.concepto_nombre, c.descripcion, c.monto_original,
           c.fecha_vencimiento, c.estado, c.fecha_generacion, c.fecha_anulacion,
           c.motivo_anulacion,
           (c.estado in ('pendiente','parcial')
             and c.monto_original - coalesce(ap.aplicado, 0) > 0
             and c.fecha_vencimiento < current_date) as es_vencido,
           round(c.monto_original - coalesce(ap.aplicado, 0), 2) as saldo,
           coalesce(ap.aplicado, 0) as aplicado
    from public.cargos c
    left join lateral (
      select sum(pa.monto_aplicado) as aplicado
      from public.pagos_aplicaciones pa
      join public.pagos pg on pg.id = pa.pago_id
      where pa.cargo_id = c.id and pa.estado = 'vigente' and pg.estado = 'registrado'
    ) ap on true
    where c.alumno_id = p_alumno_id and c.institucion_id = v
    order by c.fecha_vencimiento desc, c.orden, c.id;
end $$;

drop function if exists public.rpc_resumen_financiero_alumno(uuid, uuid);
create or replace function public.rpc_resumen_financiero_alumno(
  p_alumno_id uuid, p_institucion_id uuid default null)
returns table(alumno_id uuid, institucion_id uuid, total_obligaciones bigint,
              total_monto_original numeric, total_pendiente numeric, total_vencido numeric,
              total_anulado numeric, total_aplicado numeric)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.cargos.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if not exists(select 1 from public.alumnos a
                where a.id = p_alumno_id and a.institucion_id = v) then
    raise exception 'El alumno no existe.' using errcode = 'P0002';
  end if;
  return query
    with cargos_saldo as (
      select c.id, c.monto_original, c.fecha_vencimiento, c.estado,
             c.monto_original - coalesce(ap.aplicado, 0) as saldo,
             coalesce(ap.aplicado, 0) as aplicado
      from public.cargos c
      left join lateral (
        select sum(pa.monto_aplicado) as aplicado
        from public.pagos_aplicaciones pa
        join public.pagos pg on pg.id = pa.pago_id
        where pa.cargo_id = c.id and pa.estado = 'vigente' and pg.estado = 'registrado'
      ) ap on true
      where c.alumno_id = p_alumno_id and c.institucion_id = v
    )
    select p_alumno_id as alumno_id, v as institucion_id,
           count(*) filter (where estado in ('pendiente','parcial')
             and saldo > 0)::bigint as total_obligaciones,
           coalesce(sum(monto_original) filter (where estado <> 'anulado'), 0) as total_monto_original,
           coalesce(sum(saldo) filter (where estado in ('pendiente','parcial')
             and saldo > 0), 0) as total_pendiente,
           coalesce(sum(saldo) filter (where estado in ('pendiente','parcial')
             and saldo > 0 and fecha_vencimiento < current_date), 0) as total_vencido,
           coalesce(sum(monto_original) filter (where estado = 'anulado'), 0) as total_anulado,
           coalesce(sum(aplicado) filter (where estado <> 'anulado'), 0) as total_aplicado
    from cargos_saldo;
end $$;

-- ======================================================================
-- 13. GRANTS Y REGISTRO
-- ======================================================================
-- Funciones internas (triggers/helper): no expuestas a clientes, solo servicio.
revoke execute on function public.trg_pagos_aplicaciones_guard(),
  public.trg_cargos_no_anular_con_pagos(),
  public.trg_pagos_aplicaciones_sync_estado(),
  public.recalcular_estado_cargo(uuid) from public, anon, authenticated;
grant execute on function public.trg_pagos_aplicaciones_guard(),
  public.trg_cargos_no_anular_con_pagos(),
  public.trg_pagos_aplicaciones_sync_estado(),
  public.recalcular_estado_cargo(uuid) to service_role;

revoke execute on function public.rpc_registrar_pago(uuid,jsonb,numeric,uuid,uuid,text,text,timestamptz),
  public.rpc_listar_pagos_alumno(uuid,uuid),
  public.rpc_obtener_pago(uuid,uuid),
  public.rpc_obtener_aplicaciones_pago(uuid,uuid),
  public.rpc_anular_pago(uuid,text,uuid) from public, anon;
-- Las RPC de cargos 019 fueron drop+recreate (para anexar saldo): se re-grantean.
revoke execute on function public.rpc_listar_cargos_matricula(uuid,uuid),
  public.rpc_listar_cargos_alumno(uuid,uuid),
  public.rpc_resumen_financiero_alumno(uuid,uuid) from public, anon;
grant execute on function public.rpc_registrar_pago(uuid,jsonb,numeric,uuid,uuid,text,text,timestamptz),
  public.rpc_listar_pagos_alumno(uuid,uuid),
  public.rpc_obtener_pago(uuid,uuid),
  public.rpc_obtener_aplicaciones_pago(uuid,uuid),
  public.rpc_anular_pago(uuid,text,uuid) to authenticated;
grant execute on function public.rpc_registrar_pago(uuid,jsonb,numeric,uuid,uuid,text,text,timestamptz),
  public.rpc_listar_pagos_alumno(uuid,uuid),
  public.rpc_obtener_pago(uuid,uuid),
  public.rpc_obtener_aplicaciones_pago(uuid,uuid),
  public.rpc_anular_pago(uuid,text,uuid) to service_role;
grant execute on function public.rpc_listar_cargos_matricula(uuid,uuid),
  public.rpc_listar_cargos_alumno(uuid,uuid),
  public.rpc_resumen_financiero_alumno(uuid,uuid) to authenticated;
grant execute on function public.rpc_listar_cargos_matricula(uuid,uuid),
  public.rpc_listar_cargos_alumno(uuid,uuid),
  public.rpc_resumen_financiero_alumno(uuid,uuid) to service_role;

insert into public.schema_migrations(version, nombre, checksum)
values ('021', 'pagos_cobranza', null)
on conflict (version) do nothing;

commit;

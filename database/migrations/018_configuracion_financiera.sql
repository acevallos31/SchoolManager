-- Configuracion financiera: conceptos financieros y planes de pago.
-- Bloque 019. Solo configuracion/plantilla. La generacion de obligaciones
-- (mensualidades/pagos) pertenece al bloque 020 y no se toca aqui.
begin;
do $$ begin if not exists(select 1 from public.schema_migrations where version='017') then raise exception 'Migracion 018 requiere la migracion 017 (responsables/gestión de RPC).'; end if; end $$;

create table if not exists public.conceptos_financieros (
    id uuid primary key default gen_random_uuid(),
    institucion_id uuid not null references public.instituciones(id) on delete restrict,
    nombre text not null,
    descripcion text,
    monto numeric(12,2) not null default 0 check (monto >= 0),
    activo boolean not null default true,
    fecha_desactivacion timestamptz,
    motivo_desactivacion text,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.planes_pago (
    id uuid primary key default gen_random_uuid(),
    institucion_id uuid not null references public.instituciones(id) on delete restrict,
    nombre text not null,
    descripcion text,
    activo boolean not null default true,
    fecha_desactivacion timestamptz,
    motivo_desactivacion text,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.plan_cuotas (
    id uuid primary key default gen_random_uuid(),
    plan_id uuid not null references public.planes_pago(id) on delete cascade,
    orden integer not null check (orden >= 0),
    concepto_id uuid references public.conceptos_financieros(id) on delete restrict,
    descripcion text,
    monto numeric(12,2) not null check (monto >= 0),
    vencimiento_dias integer not null default 0 check (vencimiento_dias >= 0),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

-- El nombre del concepto es unico por institucion (insensible a mayusculas).
create unique index if not exists ux_conceptos_financieros_nombre_normalizado
  on public.conceptos_financieros(institucion_id, lower(btrim(nombre)));

-- El nombre del plan es unico por institucion.
create unique index if not exists ux_planes_pago_nombre_normalizado
  on public.planes_pago(institucion_id, lower(btrim(nombre)));

-- Cada cuota va en un orden unico dentro del plan.
create unique index if not exists ux_plan_cuotas_orden
  on public.plan_cuotas(plan_id, orden);

-- Acelera la comprobacion de la FK al actualizar/eliminar conceptos.
create index if not exists ix_plan_cuotas_concepto_id
  on public.plan_cuotas(concepto_id) where concepto_id is not null;

-- Superficie RPC-only: sin policies ni acceso directo para clientes Supabase.
alter table public.conceptos_financieros enable row level security;
alter table public.planes_pago enable row level security;
alter table public.plan_cuotas enable row level security;
revoke all privileges on table public.conceptos_financieros,
  public.planes_pago, public.plan_cuotas from public, anon, authenticated;
grant all privileges on table public.conceptos_financieros,
  public.planes_pago, public.plan_cuotas to postgres, service_role;

-- Una cuota no puede referenciar un concepto de otra institucion.
create or replace function public.trg_plan_cuotas_concepto_institucion()
returns trigger language plpgsql set search_path = pg_catalog, public, pg_temp as $$
declare v_institucion_plan uuid;
begin
  select p.institucion_id into v_institucion_plan from public.planes_pago p where p.id = new.plan_id;
  if new.concepto_id is not null and v_institucion_plan is not null
     and not exists(select 1 from public.conceptos_financieros c
                    where c.id = new.concepto_id and c.institucion_id = v_institucion_plan) then
    raise exception 'La cuota no puede referenciar un concepto de otra institucion.' using errcode = '23503';
  end if;
  return new;
end $$;

create trigger trg_plan_cuotas_concepto_institucion_before
  before insert or update of concepto_id, plan_id on public.plan_cuotas
  for each row execute function public.trg_plan_cuotas_concepto_institucion();

revoke execute on function public.trg_plan_cuotas_concepto_institucion()
  from public, anon, authenticated;
grant execute on function public.trg_plan_cuotas_concepto_institucion()
  to service_role;

insert into public.permisos(codigo,modulo,nombre) values
 ('configuracion.conceptos_financieros.ver','configuracion','Ver conceptos financieros'),
 ('configuracion.conceptos_financieros.crear','configuracion','Crear conceptos financieros'),
 ('configuracion.conceptos_financieros.editar','configuracion','Editar conceptos financieros'),
 ('configuracion.conceptos_financieros.desactivar','configuracion','Desactivar conceptos financieros'),
 ('configuracion.planes_pago.ver','configuracion','Ver planes de pago'),
 ('configuracion.planes_pago.crear','configuracion','Crear planes de pago'),
 ('configuracion.planes_pago.editar','configuracion','Editar planes de pago'),
 ('configuracion.planes_pago.desactivar','configuracion','Desactivar planes de pago')
on conflict(codigo) do nothing;
insert into public.roles_permisos(rol_id,permiso_id)
select r.id,p.id from public.roles r cross join public.permisos p where r.codigo='admin'
 and (p.codigo like 'configuracion.conceptos_financieros.%' or p.codigo like 'configuracion.planes_pago.%')
on conflict do nothing;

-- RLS: este modulo se protege exclusivamente mediante RPC SECURITY DEFINER
-- con verificacion de permisos (`usuario_tiene_permiso_actual`) y de
-- pertenencia institucional (`resolver_institucion_operacion`), igual que el
-- resto de modulos de configuracion (v. migracion 016). RLS queda habilitado
-- sin policies y se revocan todos los privilegios directos de clientes.

-- ========================= CONCEPTOS FINANCIEROS =========================

create or replace function public.rpc_listar_conceptos_financieros(
  p_institucion_id uuid default null, p_activo boolean default null)
returns table(id uuid,nombre text,descripcion text,monto numeric,activo boolean,
              fecha_desactivacion timestamptz,motivo_desactivacion text)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.conceptos_financieros.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  return query
    select c.id,c.nombre,c.descripcion,c.monto,c.activo,c.fecha_desactivacion,c.motivo_desactivacion
    from public.conceptos_financieros c
    where c.institucion_id = v
      and (p_activo is null or c.activo = p_activo)
    order by c.nombre;
end $$;

create or replace function public.rpc_crear_concepto_financiero(
  p_nombre text, p_monto numeric, p_descripcion text default null, p_institucion_id uuid default null)
returns uuid language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid; x uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.conceptos_financieros.crear', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if btrim(coalesce(p_nombre, '')) = '' then
    raise exception 'El nombre del concepto es obligatorio.' using errcode = '22023';
  end if;
  if p_monto is null or p_monto < 0 then
    raise exception 'El monto del concepto no es valido.' using errcode = '22023';
  end if;
  insert into public.conceptos_financieros(institucion_id, nombre, monto, descripcion)
  values (v, btrim(p_nombre), p_monto, nullif(btrim(coalesce(p_descripcion, '')), ''))
  returning id into x;
  return x;
end $$;

create or replace function public.rpc_actualizar_concepto_financiero(
  p_concepto_id uuid, p_nombre text, p_monto numeric, p_descripcion text default null, p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare c public.conceptos_financieros%rowtype; v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.conceptos_financieros.editar', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  select * into c from public.conceptos_financieros
  where id = p_concepto_id and institucion_id = v for update;
  if not found then raise exception 'El concepto no existe.' using errcode = 'P0002'; end if;
  if btrim(coalesce(p_nombre, '')) = '' or p_monto is null or p_monto < 0 then
    raise exception 'Nombre o monto del concepto no valido.' using errcode = '22023';
  end if;
  update public.conceptos_financieros
  set nombre = btrim(p_nombre), monto = p_monto,
      descripcion = nullif(btrim(coalesce(p_descripcion, '')), ''), updated_at = now()
  where id = c.id;
end $$;

create or replace function public.rpc_desactivar_concepto_financiero(
  p_concepto_id uuid, p_motivo text, p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare c public.conceptos_financieros%rowtype; v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.conceptos_financieros.desactivar', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  select * into c from public.conceptos_financieros
  where id = p_concepto_id and institucion_id = v for update;
  if not found then raise exception 'El concepto no existe.' using errcode = 'P0002'; end if;
  if btrim(coalesce(p_motivo, '')) = '' then
    raise exception 'El motivo es obligatorio.' using errcode = '22023';
  end if;
  update public.conceptos_financieros
  set activo = false, fecha_desactivacion = now(), motivo_desactivacion = btrim(p_motivo), updated_at = now()
  where id = c.id;
end $$;

create or replace function public.rpc_reactivar_concepto_financiero(
  p_concepto_id uuid, p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare c public.conceptos_financieros%rowtype; v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.conceptos_financieros.editar', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  select * into c from public.conceptos_financieros
  where id = p_concepto_id and institucion_id = v for update;
  if not found then raise exception 'El concepto no existe.' using errcode = 'P0002'; end if;
  update public.conceptos_financieros
  set activo = true, fecha_desactivacion = null, motivo_desactivacion = null, updated_at = now()
  where id = c.id;
end $$;

-- ============================ PLANES DE PAGO =============================

create or replace function public.rpc_listar_planes_pago(
  p_institucion_id uuid default null, p_activo boolean default null)
returns table(id uuid,nombre text,descripcion text,activo boolean,
              fecha_desactivacion timestamptz,motivo_desactivacion text,total_cuotas bigint,monto_total numeric)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.planes_pago.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  return query
    select p.id,p.nombre,p.descripcion,p.activo,p.fecha_desactivacion,p.motivo_desactivacion,
           count(c.id)::bigint, coalesce(sum(c.monto), 0)
    from public.planes_pago p
    left join public.plan_cuotas c on c.plan_id = p.id
    where p.institucion_id = v
      and (p_activo is null or p.activo = p_activo)
    group by p.id
    order by p.nombre;
end $$;

create or replace function public.rpc_obtener_plan_pago(p_plan_id uuid, p_institucion_id uuid default null)
returns table(id uuid,nombre text,descripcion text,activo boolean,
              cuota_id uuid,cuota_orden integer,concepto_id uuid,concepto_nombre text,
              cuota_descripcion text,cuota_monto numeric,vencimiento_dias integer)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid; vp public.planes_pago%rowtype;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.planes_pago.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  select * into vp from public.planes_pago p
  where p.id = p_plan_id and p.institucion_id = v;
  if not found then raise exception 'El plan no existe.' using errcode = 'P0002'; end if;
  return query
    select vp.id,vp.nombre,vp.descripcion,vp.activo,
           c.id,c.orden,c.concepto_id,coalesce(k.nombre, ''),c.descripcion,c.monto,c.vencimiento_dias
    from public.planes_pago p
    left join public.plan_cuotas c on c.plan_id = p.id
    left join public.conceptos_financieros k on k.id = c.concepto_id
    where p.id = p_plan_id
    order by c.orden;
end $$;

-- Inserta/actualiza las cuotas de un plan de forma atomica.
create or replace function public.rpc_crear_plan_pago(
  p_nombre text, p_descripcion text, p_cuotas jsonb, p_institucion_id uuid default null)
returns uuid language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid; x uuid; c jsonb;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.planes_pago.crear', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if btrim(coalesce(p_nombre, '')) = '' then
    raise exception 'El nombre del plan es obligatorio.' using errcode = '22023';
  end if;
  if p_cuotas is null or jsonb_typeof(p_cuotas) <> 'array' then
    raise exception 'Las cuotas deben enviarse como un arreglo JSON.' using errcode = '22023';
  end if;
  if jsonb_array_length(p_cuotas) = 0 then
    raise exception 'El plan debe incluir al menos una cuota.' using errcode = '22023';
  end if;

  insert into public.planes_pago(institucion_id, nombre, descripcion)
  values (v, btrim(p_nombre), nullif(btrim(coalesce(p_descripcion, '')), ''))
  returning id into x;

  for c in select * from jsonb_array_elements(p_cuotas)
  loop
    if jsonb_typeof(c) <> 'object'
       or (c->>'monto') is null or (c->>'monto')::numeric < 0
       or coalesce((c->>'orden')::integer, 0) < 0
       or coalesce((c->>'vencimiento_dias')::integer, 0) < 0 then
      raise exception 'Los datos de una cuota no son validos.' using errcode = '22023';
    end if;
    insert into public.plan_cuotas(plan_id, orden, concepto_id, descripcion, monto, vencimiento_dias)
    values (x,
            coalesce((c->>'orden')::integer, 0),
            (c->>'concepto_id')::uuid,
            nullif(btrim(coalesce(c->>'descripcion', '')), ''),
            (c->>'monto')::numeric,
            coalesce((c->>'vencimiento_dias')::integer, 0));
  end loop;
  return x;
end $$;

create or replace function public.rpc_actualizar_plan_pago(
  p_plan_id uuid, p_nombre text, p_descripcion text, p_cuotas jsonb, p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid; p public.planes_pago%rowtype; c jsonb;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.planes_pago.editar', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  select * into p from public.planes_pago
  where id = p_plan_id and institucion_id = v for update;
  if not found then raise exception 'El plan no existe.' using errcode = 'P0002'; end if;
  if btrim(coalesce(p_nombre, '')) = '' then
    raise exception 'El nombre del plan es obligatorio.' using errcode = '22023';
  end if;
  if p.activo is false then
    raise exception 'Un plan desactivado no puede editarse.' using errcode = '23514';
  end if;
  if p_cuotas is null or jsonb_typeof(p_cuotas) <> 'array' then
    raise exception 'Las cuotas deben enviarse como un arreglo JSON.' using errcode = '22023';
  end if;
  if jsonb_array_length(p_cuotas) = 0 then
    raise exception 'El plan debe incluir al menos una cuota.' using errcode = '22023';
  end if;

  update public.planes_pago
  set nombre = btrim(p_nombre),
      descripcion = nullif(btrim(coalesce(p_descripcion, '')), ''), updated_at = now()
  where id = p.id;

  -- Reemplazo atomico de cuotas.
  delete from public.plan_cuotas where plan_id = p.id;
  for c in select * from jsonb_array_elements(p_cuotas)
  loop
    if jsonb_typeof(c) <> 'object'
       or (c->>'monto') is null or (c->>'monto')::numeric < 0
       or coalesce((c->>'orden')::integer, 0) < 0
       or coalesce((c->>'vencimiento_dias')::integer, 0) < 0 then
      raise exception 'Los datos de una cuota no son validos.' using errcode = '22023';
    end if;
    insert into public.plan_cuotas(plan_id, orden, concepto_id, descripcion, monto, vencimiento_dias)
    values (p.id,
            coalesce((c->>'orden')::integer, 0),
            (c->>'concepto_id')::uuid,
            nullif(btrim(coalesce(c->>'descripcion', '')), ''),
            (c->>'monto')::numeric,
            coalesce((c->>'vencimiento_dias')::integer, 0));
  end loop;
end $$;

create or replace function public.rpc_desactivar_plan_pago(
  p_plan_id uuid, p_motivo text, p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare p public.planes_pago%rowtype; v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.planes_pago.desactivar', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  select * into p from public.planes_pago
  where id = p_plan_id and institucion_id = v for update;
  if not found then raise exception 'El plan no existe.' using errcode = 'P0002'; end if;
  if btrim(coalesce(p_motivo, '')) = '' then
    raise exception 'El motivo es obligatorio.' using errcode = '22023';
  end if;
  update public.planes_pago
  set activo = false, fecha_desactivacion = now(), motivo_desactivacion = btrim(p_motivo), updated_at = now()
  where id = p.id;
end $$;

create or replace function public.rpc_reactivar_plan_pago(
  p_plan_id uuid, p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare p public.planes_pago%rowtype; v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.planes_pago.editar', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  select * into p from public.planes_pago
  where id = p_plan_id and institucion_id = v for update;
  if not found then raise exception 'El plan no existe.' using errcode = 'P0002'; end if;
  update public.planes_pago
  set activo = true, fecha_desactivacion = null, motivo_desactivacion = null, updated_at = now()
  where id = p.id;
end $$;

-- ======================= GRANTS Y REGISTRO ================================

revoke execute on function public.rpc_listar_conceptos_financieros(uuid,boolean),
  public.rpc_crear_concepto_financiero(text,numeric,text,uuid),
  public.rpc_actualizar_concepto_financiero(uuid,text,numeric,text,uuid),
  public.rpc_desactivar_concepto_financiero(uuid,text,uuid),
  public.rpc_reactivar_concepto_financiero(uuid,uuid),
  public.rpc_listar_planes_pago(uuid,boolean),
  public.rpc_obtener_plan_pago(uuid,uuid),
  public.rpc_crear_plan_pago(text,text,jsonb,uuid),
  public.rpc_actualizar_plan_pago(uuid,text,text,jsonb,uuid),
  public.rpc_desactivar_plan_pago(uuid,text,uuid),
  public.rpc_reactivar_plan_pago(uuid,uuid) from public, anon;
grant execute on function public.rpc_listar_conceptos_financieros(uuid,boolean),
  public.rpc_crear_concepto_financiero(text,numeric,text,uuid),
  public.rpc_actualizar_concepto_financiero(uuid,text,numeric,text,uuid),
  public.rpc_desactivar_concepto_financiero(uuid,text,uuid),
  public.rpc_reactivar_concepto_financiero(uuid,uuid),
  public.rpc_listar_planes_pago(uuid,boolean),
  public.rpc_obtener_plan_pago(uuid,uuid),
  public.rpc_crear_plan_pago(text,text,jsonb,uuid),
  public.rpc_actualizar_plan_pago(uuid,text,text,jsonb,uuid),
  public.rpc_desactivar_plan_pago(uuid,text,uuid),
  public.rpc_reactivar_plan_pago(uuid,uuid) to authenticated;
grant execute on function public.rpc_listar_conceptos_financieros(uuid,boolean),
  public.rpc_crear_concepto_financiero(text,numeric,text,uuid),
  public.rpc_actualizar_concepto_financiero(uuid,text,numeric,text,uuid),
  public.rpc_desactivar_concepto_financiero(uuid,text,uuid),
  public.rpc_reactivar_concepto_financiero(uuid,uuid),
  public.rpc_listar_planes_pago(uuid,boolean),
  public.rpc_obtener_plan_pago(uuid,uuid),
  public.rpc_crear_plan_pago(text,text,jsonb,uuid),
  public.rpc_actualizar_plan_pago(uuid,text,text,jsonb,uuid),
  public.rpc_desactivar_plan_pago(uuid,text,uuid),
  public.rpc_reactivar_plan_pago(uuid,uuid) to service_role;
insert into public.schema_migrations(version,nombre,checksum)values('018','configuracion_financiera',null)on conflict(version)do nothing;
commit;

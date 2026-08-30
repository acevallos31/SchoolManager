-- Configuracion funcional global de una implementacion SchoolManager.
-- El modo single es el valor predeterminado; institucion_id permanece en el modelo.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '011') then
    raise exception 'Migracion 012 requiere la migracion 011.';
  end if;
end;
$$;

create table public.configuracion_implementacion (
  id smallint primary key default 1,
  multiples_instituciones boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint ck_configuracion_implementacion_singleton check (id = 1)
);

insert into public.configuracion_implementacion (id) values (1);

insert into public.permisos (codigo, modulo, nombre)
values
  ('configuracion.sistema.ver', 'configuracion', 'Ver configuracion del sistema'),
  ('configuracion.sistema.editar', 'configuracion', 'Editar configuracion del sistema'),
  ('configuracion.instituciones.ver', 'configuracion', 'Ver instituciones'),
  ('configuracion.instituciones.editar', 'configuracion', 'Editar instituciones')
on conflict (codigo) do nothing;

insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'admin'
  and p.codigo like 'configuracion.%'
on conflict do nothing;

create or replace function public.resolver_contexto_institucional()
returns table (
  multiples_instituciones boolean,
  institucion_id uuid,
  institucion_nombre text
)
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare
  v_multi boolean;
  v_cantidad integer;
begin
  select c.multiples_instituciones
  into v_multi
  from public.configuracion_implementacion c
  where c.id = 1;

  if not found then
    raise exception 'CONFIGURATION_REQUIRED: falta la configuracion de implementacion.'
      using errcode = 'SM001';
  end if;

  if v_multi then
    return query select true, null::uuid, null::text;
    return;
  end if;

  select count(*)::integer into v_cantidad
  from public.instituciones i where i.activo;

  if v_cantidad = 0 then
    raise exception 'NO_INSTITUTION_CONFIGURED: no hay una institucion activa.'
      using errcode = 'SM001';
  end if;
  if v_cantidad > 1 then
    raise exception 'MULTIPLE_INSTITUTIONS_IN_SINGLE_MODE: hay varias instituciones activas.'
      using errcode = 'SM002';
  end if;

  return query
  select false, i.id, i.nombre
  from public.instituciones i
  where i.activo;
end;
$$;

create or replace function public.rpc_obtener_contexto_implementacion()
returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_contexto record;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  select * into v_contexto from public.resolver_contexto_institucional();
  return jsonb_build_object(
    'multiplesInstituciones', v_contexto.multiples_instituciones,
    'institucion', case
      when v_contexto.institucion_id is null then null
      else jsonb_build_object(
        'id', v_contexto.institucion_id,
        'nombre', v_contexto.institucion_nombre
      )
    end
  );
end;
$$;

create or replace function public.rpc_actualizar_multiples_instituciones(
  p_multiples_instituciones boolean
)
returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_activas integer;
begin
  if public.usuario_actual_id() is null
     or not public.usuario_tiene_permiso_actual('configuracion.sistema.editar', null) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  if not p_multiples_instituciones then
    select count(*)::integer into v_activas
    from public.instituciones where activo;
    if v_activas > 1 then
      raise exception 'MULTIPLE_INSTITUTIONS_IN_SINGLE_MODE: desactive instituciones antes de usar modo single.'
        using errcode = 'SM002';
    end if;
  end if;

  update public.configuracion_implementacion
  set multiples_instituciones = p_multiples_instituciones,
      updated_at = now()
  where id = 1;

  return public.rpc_obtener_contexto_implementacion();
end;
$$;

alter table public.configuracion_implementacion enable row level security;

revoke all privileges on public.configuracion_implementacion from public, anon, authenticated;
grant all privileges on public.configuracion_implementacion to service_role;

revoke execute on function public.resolver_contexto_institucional() from public, anon, authenticated;
revoke execute on function public.rpc_obtener_contexto_implementacion() from public, anon;
revoke execute on function public.rpc_actualizar_multiples_instituciones(boolean) from public, anon;
grant execute on function public.rpc_obtener_contexto_implementacion() to authenticated;
grant execute on function public.rpc_actualizar_multiples_instituciones(boolean) to authenticated;
grant execute on function public.resolver_contexto_institucional() to service_role;
grant execute on function public.rpc_obtener_contexto_implementacion() to service_role;
grant execute on function public.rpc_actualizar_multiples_instituciones(boolean) to service_role;

insert into public.schema_migrations (version, nombre, checksum)
values ('012', 'configuracion_implementacion', null)
on conflict (version) do nothing;

commit;

begin;

drop function if exists public.rpc_listar_grados(uuid),public.rpc_crear_grado(text,integer,uuid),public.rpc_actualizar_grado(uuid,text,integer,uuid),public.rpc_cambiar_estado_grado(uuid,boolean,uuid),public.rpc_desactivar_grado(uuid,uuid),public.rpc_reactivar_grado(uuid,uuid);
drop function if exists public.rpc_listar_jornadas(uuid),public.rpc_crear_jornada(text,uuid),public.rpc_actualizar_jornada(uuid,text,uuid),public.rpc_cambiar_estado_jornada(uuid,boolean,uuid),public.rpc_desactivar_jornada(uuid,uuid),public.rpc_reactivar_jornada(uuid,uuid);
drop function if exists public.rpc_listar_secciones(uuid,uuid),public.rpc_actualizar_seccion(uuid,uuid,uuid,uuid,text,integer,uuid),public.rpc_reactivar_seccion(uuid,uuid);

create or replace function public.rpc_crear_seccion(
  p_institucion_id uuid, p_ciclo_id uuid, p_grado_id uuid,
  p_jornada_id uuid, p_nombre text, p_cupo integer default null
)
returns uuid language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
begin
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.secciones.crear', p_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  return public.crear_seccion(
    p_institucion_id, p_ciclo_id, p_grado_id, p_jornada_id, p_nombre, p_cupo);
end;
$$;

drop function if exists public.rpc_desactivar_seccion(uuid,text,uuid);
create or replace function public.rpc_desactivar_seccion(p_seccion_id uuid, p_motivo text)
returns void language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid;
begin
  select institucion_id into v_institucion_id from public.secciones where id = p_seccion_id;
  if public.usuario_actual_id() is null or v_institucion_id is null
     or not public.usuario_tiene_permiso_actual(
       'academico.secciones.desactivar', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  perform public.desactivar_seccion(p_seccion_id, p_motivo);
end;
$$;

drop index if exists public.ux_grados_nombre_normalizado;
drop index if exists public.ux_jornadas_nombre_normalizado;

-- Revierte la regla de unicidad introducida por 016.
drop index if exists public.ux_secciones_contexto_nombre;

-- Restaura los indices originales definidos antes de 016.
create unique index if not exists ux_secciones_contexto_jornada_nombre
  on public.secciones(
    institucion_id,
    ciclo_id,
    grado_id,
    jornada_id,
    lower(nombre)
  )
  where jornada_id is not null;

create unique index if not exists ux_secciones_contexto_sin_jornada_nombre
  on public.secciones(
    institucion_id,
    ciclo_id,
    grado_id,
    lower(nombre)
  )
  where jornada_id is null;

delete from public.roles_permisos rp
using public.permisos p
where rp.permiso_id=p.id
  and (
    p.codigo like 'configuracion.grados.%'
    or p.codigo like 'configuracion.jornadas.%'
    or p.codigo like 'configuracion.secciones.%'
  );

delete from public.permisos
where codigo like 'configuracion.grados.%'
   or codigo like 'configuracion.jornadas.%'
   or codigo like 'configuracion.secciones.%';

delete from public.schema_migrations
where version='016';

commit;
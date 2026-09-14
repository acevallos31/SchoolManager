-- Migracion 033 - asignacion de roles institucionales.
begin;

do $$ begin
  if not exists (select 1 from public.schema_migrations where version='032') then
    raise exception 'La migracion 033 requiere la 032 aplicada previamente.';
  end if;
end $$;

create or replace function public.rpc_asignar_rol_institucional(
  p_usuario_id uuid,
  p_rol_id uuid
)
returns uuid language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare v_inst uuid; v_id uuid;
begin
  select institucion_id into v_inst
  from public.roles
  where id=p_rol_id and tipo='institucional' and activo;

  if not found then
    raise exception 'El rol institucional no existe o esta inactivo.' using errcode='P0002';
  end if;

  if public.usuario_actual_id() is null
     or not public.usuario_tiene_permiso_actual('identidad.usuarios.asignar_roles',v_inst) then
    raise exception 'Permiso denegado.' using errcode='42501';
  end if;

  if not exists(select 1 from public.usuarios where id=p_usuario_id and activo) then
    raise exception 'El usuario destino no existe o esta inactivo.' using errcode='23503';
  end if;

  insert into public.usuarios_roles(usuario_id,rol_id,institucion_id)
  values(p_usuario_id,p_rol_id,v_inst)
  returning id into v_id;

  insert into public.seguridad_auditoria(
    actor_usuario_id,institucion_id,accion,entidad_tipo,entidad_id,detalle
  ) values(
    public.usuario_actual_id(),v_inst,'rol_usuario.asignar','usuario_rol',v_id,
    jsonb_build_object('usuario_id',p_usuario_id,'rol_id',p_rol_id)
  );

  return v_id;
end $$;

revoke execute on function public.rpc_asignar_rol_institucional(uuid,uuid) from public,anon;
grant execute on function public.rpc_asignar_rol_institucional(uuid,uuid) to authenticated,service_role;

insert into public.schema_migrations(version,nombre,checksum)
values('033','roles_institucionales_asignar',null)
on conflict(version) do nothing;

commit;

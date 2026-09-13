-- Migracion 034 - desactivacion de roles institucionales.
begin;

do $$ begin
  if not exists (select 1 from public.schema_migrations where version='033') then
    raise exception 'La migracion 034 requiere la 033 aplicada previamente.';
  end if;
end $$;

create or replace function public.rpc_desactivar_rol_institucional(
  p_rol_id uuid,
  p_motivo text
)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare v_inst uuid; v_antes integer; v_despues integer;
begin
  if btrim(coalesce(p_motivo,''))='' then
    raise exception 'El motivo es obligatorio.' using errcode='22023';
  end if;

  select institucion_id into v_inst
  from public.roles
  where id=p_rol_id and tipo='institucional' and activo and not protegido
  for update;

  if not found then
    raise exception 'El rol institucional no existe, esta inactivo o esta protegido.' using errcode='P0002';
  end if;

  if public.usuario_actual_id() is null
     or not public.usuario_tiene_permiso_actual('identidad.roles.editar',v_inst) then
    raise exception 'Permiso denegado.' using errcode='42501';
  end if;

  perform pg_advisory_xact_lock(
    hashtextextended('schoolmanager:admin-institucional:'||v_inst::text,0)
  );
  v_antes:=public.contar_admins_institucionales(v_inst);

  update public.roles set activo=false,updated_at=now() where id=p_rol_id;
  update public.usuarios_roles
  set activo=false,fecha_desactivacion=now(),motivo_desactivacion=btrim(p_motivo),updated_at=now()
  where rol_id=p_rol_id and activo;

  v_despues:=public.contar_admins_institucionales(v_inst);
  if v_antes>0 and v_despues=0 then
    raise exception 'No se puede desactivar el rol del ultimo administrador institucional activo.' using errcode='23514';
  end if;

  insert into public.seguridad_auditoria(actor_usuario_id,institucion_id,accion,entidad_tipo,entidad_id,detalle)
  values(public.usuario_actual_id(),v_inst,'rol_institucional.desactivar','rol',p_rol_id,jsonb_build_object('motivo',btrim(p_motivo)));
end $$;

revoke execute on function public.rpc_desactivar_rol_institucional(uuid,text) from public,anon;
grant execute on function public.rpc_desactivar_rol_institucional(uuid,text) to authenticated,service_role;

insert into public.schema_migrations(version,nombre,checksum)
values('034','roles_institucionales_desactivar',null)
on conflict(version) do nothing;

commit;

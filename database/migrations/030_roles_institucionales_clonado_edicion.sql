-- Migracion 030 - clonado y edicion de roles institucionales.
begin;

do $$ begin
  if not exists (select 1 from public.schema_migrations where version='029') then
    raise exception 'La migracion 030 requiere la 029 aplicada previamente.';
  end if;
end $$;

create or replace function public.rpc_clonar_plantilla_rol(
  p_institucion_id uuid,
  p_plantilla_codigo text,
  p_codigo text,
  p_nombre text,
  p_descripcion text default null
)
returns uuid language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare
  v_base uuid;
  v_version integer;
  v_id uuid;
  v_codigo text:=lower(btrim(coalesce(p_codigo,'')));
begin
  if public.usuario_actual_id() is null
     or not public.usuario_tiene_permiso_actual('identidad.roles.crear',p_institucion_id) then
    raise exception 'Permiso denegado.' using errcode='42501';
  end if;

  select id,plantilla_version into v_base,v_version
  from public.roles
  where institucion_id is null
    and tipo='plantilla'
    and activo
    and codigo=lower(btrim(coalesce(p_plantilla_codigo,'')));

  if v_base is null then
    raise exception 'La plantilla no existe o esta inactiva.' using errcode='P0002';
  end if;

  if exists(
    select 1
    from public.roles_permisos rp
    join public.permisos p on p.id=rp.permiso_id
    where rp.rol_id=v_base
      and p.ambito='institucion'
      and p.delegable
      and p.estado='vigente'
      and not public.usuario_tiene_permiso_actual(p.codigo,p_institucion_id)
  ) then
    raise exception 'La plantilla contiene permisos que el usuario no puede delegar.' using errcode='42501';
  end if;

  if v_codigo='' or btrim(coalesce(p_nombre,''))='' then
    raise exception 'Codigo y nombre son obligatorios.' using errcode='22023';
  end if;

  if exists(
    select 1 from public.roles
    where institucion_id is null and tipo='plataforma' and codigo=v_codigo
  ) then
    raise exception 'Codigo reservado para plataforma.' using errcode='23514';
  end if;

  insert into public.roles(
    codigo,nombre,descripcion,es_sistema,activo,institucion_id,tipo,protegido,rol_base_id,plantilla_version
  ) values(
    v_codigo,btrim(p_nombre),nullif(btrim(coalesce(p_descripcion,'')),''),false,true,
    p_institucion_id,'institucional',false,v_base,v_version
  ) returning id into v_id;

  insert into public.roles_permisos(rol_id,permiso_id)
  select v_id,p.id
  from public.roles_permisos rp
  join public.permisos p on p.id=rp.permiso_id
  where rp.rol_id=v_base
    and p.ambito='institucion'
    and p.delegable
    and p.estado='vigente'
  on conflict do nothing;

  insert into public.seguridad_auditoria(actor_usuario_id,institucion_id,accion,entidad_tipo,entidad_id,detalle)
  values(
    public.usuario_actual_id(),p_institucion_id,'rol_institucional.clonar_plantilla','rol',v_id,
    jsonb_build_object('plantilla_id',v_base,'plantilla_codigo',lower(btrim(p_plantilla_codigo)))
  );

  return v_id;
end $$;

create or replace function public.rpc_editar_rol_institucional(
  p_rol_id uuid,p_nombre text,p_descripcion text default null
)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare v_inst uuid;
begin
  select institucion_id into v_inst
  from public.roles
  where id=p_rol_id and tipo='institucional' and activo
  for update;

  if not found then
    raise exception 'El rol institucional no existe o esta inactivo.' using errcode='P0002';
  end if;

  if public.usuario_actual_id() is null
     or not public.usuario_tiene_permiso_actual('identidad.roles.editar',v_inst) then
    raise exception 'Permiso denegado.' using errcode='42501';
  end if;

  if btrim(coalesce(p_nombre,''))='' then
    raise exception 'El nombre es obligatorio.' using errcode='22023';
  end if;

  update public.roles
  set nombre=btrim(p_nombre),
      descripcion=nullif(btrim(coalesce(p_descripcion,'')),''),
      updated_at=now()
  where id=p_rol_id;

  insert into public.seguridad_auditoria(actor_usuario_id,institucion_id,accion,entidad_tipo,entidad_id,detalle)
  values(public.usuario_actual_id(),v_inst,'rol_institucional.editar','rol',p_rol_id,jsonb_build_object('nombre',btrim(p_nombre)));
end $$;

revoke execute on function public.rpc_clonar_plantilla_rol(uuid,text,text,text,text) from public,anon;
revoke execute on function public.rpc_editar_rol_institucional(uuid,text,text) from public,anon;
grant execute on function public.rpc_clonar_plantilla_rol(uuid,text,text,text,text) to authenticated,service_role;
grant execute on function public.rpc_editar_rol_institucional(uuid,text,text) to authenticated,service_role;

insert into public.schema_migrations(version,nombre,checksum)
values('030','roles_institucionales_clonado_edicion',null)
on conflict(version) do nothing;

commit;

-- Migracion 032 - reemplazo transaccional de permisos institucionales.
begin;

do $$ begin
  if not exists (select 1 from public.schema_migrations where version='031') then
    raise exception 'La migracion 032 requiere la 031 aplicada previamente.';
  end if;
end $$;

create or replace function public.rpc_reemplazar_permisos_rol_institucional(
  p_rol_id uuid,
  p_permiso_codigos text[]
)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare
  v_inst uuid;
  v_codigos text[];
  v_antes integer;
  v_despues integer;
begin
  select institucion_id into v_inst
  from public.roles
  where id=p_rol_id and tipo='institucional' and activo
  for update;
  if not found then
    raise exception 'El rol institucional no existe o esta inactivo.' using errcode='P0002';
  end if;
  if public.usuario_actual_id() is null
     or not public.usuario_tiene_permiso_actual('identidad.roles.asignar_permisos',v_inst) then
    raise exception 'Permiso denegado.' using errcode='42501';
  end if;

  select coalesce(array_agg(c order by c),'{}'::text[]) into v_codigos
  from (
    select distinct lower(btrim(x)) c
    from unnest(coalesce(p_permiso_codigos,'{}'::text[])) t(x)
    where x is not null and btrim(x)<>''
  ) s;

  if exists(
    select 1
    from unnest(v_codigos) c
    left join public.permisos p on p.codigo=c
    where p.id is null
       or p.ambito<>'institucion'
       or not p.delegable
       or p.estado<>'vigente'
  ) then
    raise exception 'La seleccion contiene permisos inexistentes o no delegables.' using errcode='23514';
  end if;

  if exists(
    select 1
    from unnest(v_codigos) c
    join public.permisos p on p.codigo=c
    where not public.usuario_tiene_permiso_actual(p.codigo,v_inst)
  ) then
    raise exception 'No se puede delegar un permiso que el usuario no posee.' using errcode='42501';
  end if;

  perform pg_advisory_xact_lock(
    hashtextextended('schoolmanager:admin-institucional:'||v_inst::text,0)
  );
  v_antes:=public.contar_admins_institucionales(v_inst);
  delete from public.roles_permisos where rol_id=p_rol_id;
  insert into public.roles_permisos(rol_id,permiso_id)
  select p_rol_id,id from public.permisos where codigo=any(v_codigos)
  on conflict do nothing;
  update public.roles set updated_at=now() where id=p_rol_id;

  v_despues:=public.contar_admins_institucionales(v_inst);
  if v_antes>0 and v_despues=0 then
    raise exception 'No se puede eliminar al ultimo administrador institucional activo.' using errcode='23514';
  end if;

  insert into public.seguridad_auditoria(
    actor_usuario_id,institucion_id,accion,entidad_tipo,entidad_id,detalle
  ) values(
    public.usuario_actual_id(),v_inst,'rol_institucional.reemplazar_permisos','rol',p_rol_id,
    jsonb_build_object('permisos',to_jsonb(v_codigos))
  );
end $$;

revoke execute on function public.rpc_reemplazar_permisos_rol_institucional(uuid,text[]) from public,anon;
grant execute on function public.rpc_reemplazar_permisos_rol_institucional(uuid,text[]) to authenticated,service_role;

insert into public.schema_migrations(version,nombre,checksum)
values('032','roles_institucionales_reemplazar_permisos',null)
on conflict(version) do nothing;

commit;

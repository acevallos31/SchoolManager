-- Bootstrap manual del PR 042: primer Superadministrador de plataforma.
--
-- NO es una migracion y NO debe ejecutarse automaticamente.
-- Solo se usa durante el rollout, despues de aplicar y validar 028..038.
-- El script se niega a operar si ya existe un Superadministrador activo; a
-- partir de ese momento las altas/bajas deben pasar por las reglas normales.
--
-- Preparacion de la misma sesion SQL:
--   set schoolmanager.bootstrap_auth_user_id = '<AUTH_USER_ID_UUID>';
-- Luego ejecutar este archivo completo.

begin;

do $$
declare
  v_auth_raw text := nullif(btrim(current_setting('schoolmanager.bootstrap_auth_user_id', true)), '');
  v_auth_user_id uuid;
  v_usuario_id uuid;
  v_rol_id uuid;
  v_asignacion_id uuid;
  v_superadmins integer;
begin
  if v_auth_raw is null then
    raise exception 'Defina schoolmanager.bootstrap_auth_user_id antes de ejecutar el bootstrap.'
      using errcode = '22023';
  end if;

  begin
    v_auth_user_id := v_auth_raw::uuid;
  exception when invalid_text_representation then
    raise exception 'schoolmanager.bootstrap_auth_user_id no es un UUID valido.'
      using errcode = '22023';
  end;

  if not exists (
    select 1
    from public.schema_migrations
    where version = '038'
  ) then
    raise exception 'El bootstrap requiere las migraciones 028..038 aplicadas y validadas.'
      using errcode = '55000';
  end if;

  perform pg_advisory_xact_lock(hashtextextended('schoolmanager:superadmin', 0));

  select u.id
  into v_usuario_id
  from public.usuarios u
  where u.auth_user_id = v_auth_user_id
    and u.activo
  for update;

  if not found then
    raise exception 'No existe un usuario interno activo vinculado al auth_user_id indicado.'
      using errcode = 'P0002';
  end if;

  select r.id
  into v_rol_id
  from public.roles r
  where r.codigo = 'platform_admin'
    and r.tipo = 'plataforma'
    and r.institucion_id is null
    and r.activo
    and r.protegido
  for update;

  if not found then
    raise exception 'El rol protegido platform_admin no existe o no esta activo.'
      using errcode = 'P0002';
  end if;

  select count(*)::integer
  into v_superadmins
  from public.usuarios_roles ur
  join public.roles r on r.id = ur.rol_id
  join public.usuarios u on u.id = ur.usuario_id
  where ur.activo
    and ur.institucion_id is null
    and r.activo
    and r.tipo = 'plataforma'
    and r.codigo = 'platform_admin'
    and u.activo;

  if v_superadmins <> 0 then
    raise exception 'El bootstrap inicial esta cerrado: ya existe al menos un Superadministrador activo.'
      using errcode = '23514';
  end if;

  insert into public.usuarios_roles(usuario_id, rol_id, institucion_id, activo)
  values(v_usuario_id, v_rol_id, null, true)
  returning id into v_asignacion_id;

  insert into public.seguridad_auditoria(
    actor_usuario_id,
    institucion_id,
    accion,
    entidad_tipo,
    entidad_id,
    detalle
  )
  values(
    null,
    null,
    'platform.superadmin.bootstrap_inicial',
    'usuario_rol',
    v_asignacion_id,
    jsonb_build_object(
      'usuario_id', v_usuario_id,
      'rol_codigo', 'platform_admin',
      'origen', 'database/operations/bootstrap_first_platform_admin.sql'
    )
  );

  raise notice 'Bootstrap completado. usuario_id=%, asignacion_id=%',
    v_usuario_id, v_asignacion_id;
end
$$;

commit;
reset schoolmanager.bootstrap_auth_user_id;

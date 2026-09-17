-- Migracion 042 - invitacion de acceso para responsable existente.
-- 046D: usa responsable_id -> persona_id de forma explicita; nunca resuelve
-- la Persona objetivo por coincidencia de correo.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '041') then
    raise exception 'La migracion 042 requiere la 041 aplicada previamente.';
  end if;
end
$$;

create or replace function public.rpc_preparar_invitacion_responsable(
  p_responsable_id uuid
)
returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_actor uuid := public.usuario_actual_id();
  v_institucion_id uuid;
  v_persona_id uuid;
  v_responsable_estado text;
  v_persona_estado text;
  v_correo text;
  v_usuario_id uuid;
  v_auth_user_id uuid;
  v_usuario_activo boolean;
  v_plantilla_id uuid;
  v_rol_id uuid;
  v_rol_activo boolean;
  v_rol_base_id uuid;
  v_asignacion_id uuid;
  v_invitacion_id uuid;
  v_invitacion_rol_id uuid;
  v_creo_usuario boolean := false;
  v_creo_rol boolean := false;
  v_creo_asignacion boolean := false;
  v_creo_invitacion boolean := false;
begin
  if v_actor is null then
    raise exception 'Usuario autenticado no vinculado o inactivo.' using errcode = '42501';
  end if;
  if p_responsable_id is null then
    raise exception 'El responsable es obligatorio.' using errcode = '22023';
  end if;

  select r.institucion_id, r.persona_id, r.estado, p.estado,
         lower(btrim(coalesce(p.correo, '')))
    into v_institucion_id, v_persona_id, v_responsable_estado,
         v_persona_estado, v_correo
  from public.responsables r
  join public.personas p on p.id = r.persona_id
  where r.id = p_responsable_id
  for update of r, p;

  if v_persona_id is null then
    raise exception 'El responsable no existe.' using errcode = '23503';
  end if;
  if v_responsable_estado <> 'activo' or v_persona_estado <> 'activo' then
    raise exception 'El responsable o su persona estan inactivos.' using errcode = 'P0001';
  end if;
  if not exists (
    select 1 from public.instituciones i
    where i.id = v_institucion_id and i.activo
  ) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;
  if v_correo = '' then
    raise exception 'El responsable debe tener un correo antes de preparar el acceso.' using errcode = '22023';
  end if;

  if not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.crear', v_institucion_id
  ) or not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.asignar_roles', v_institucion_id
  ) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  -- Un lock por responsable evita duplicar usuario/invitacion. Un segundo lock
  -- por institucion serializa la creacion perezosa del unico rol parent local.
  perform pg_catalog.pg_advisory_xact_lock(
    pg_catalog.hashtextextended('responsable:' || p_responsable_id::text, 0)
  );
  perform pg_catalog.pg_advisory_xact_lock(
    pg_catalog.hashtextextended('parent-role:' || v_institucion_id::text, 0)
  );

  select r.id
    into v_plantilla_id
  from public.roles r
  where r.codigo = 'parent'
    and r.tipo = 'plantilla'
    and r.institucion_id is null
    and r.activo
  limit 1;

  if v_plantilla_id is null then
    raise exception 'La plantilla global parent no esta disponible.' using errcode = '23503';
  end if;

  select r.id, r.activo, r.rol_base_id
    into v_rol_id, v_rol_activo, v_rol_base_id
  from public.roles r
  where r.institucion_id = v_institucion_id
    and r.codigo = 'parent'
    and r.tipo = 'institucional'
  limit 1
  for update;

  if v_rol_id is null then
    if not public.usuario_tiene_permiso_institucional_estricto(
      'identidad.roles.crear', v_institucion_id
    ) then
      raise exception 'El rol Padre o responsable aun no existe y el actor no puede crearlo.'
        using errcode = '42501';
    end if;

    if exists (
      select 1 from public.roles_permisos rp where rp.rol_id = v_plantilla_id
    ) and not public.usuario_tiene_permiso_institucional_estricto(
      'identidad.roles.asignar_permisos', v_institucion_id
    ) then
      raise exception 'El rol Padre o responsable requiere permisos que el actor no puede administrar.'
        using errcode = '42501';
    end if;

    if exists (
      select 1
      from public.roles_permisos rp
      join public.permisos p on p.id = rp.permiso_id
      where rp.rol_id = v_plantilla_id
        and p.estado = 'vigente'
        and (
          p.ambito <> 'institucion'
          or not p.delegable
          or not public.usuario_tiene_permiso_institucional_estricto(p.codigo, v_institucion_id)
        )
    ) then
      raise exception 'La plantilla parent contiene permisos que el actor no puede delegar.'
        using errcode = '42501';
    end if;

    insert into public.roles(
      codigo, nombre, descripcion, es_sistema, activo, institucion_id,
      tipo, protegido, rol_base_id, plantilla_version
    )
    select
      'parent', r.nombre, r.descripcion, false, true, v_institucion_id,
      'institucional', false, r.id, r.plantilla_version
    from public.roles r
    where r.id = v_plantilla_id
    returning id into v_rol_id;
    v_creo_rol := true;

    insert into public.roles_permisos(rol_id, permiso_id)
    select v_rol_id, rp.permiso_id
    from public.roles_permisos rp
    where rp.rol_id = v_plantilla_id
    on conflict do nothing;
  else
    if not v_rol_activo then
      raise exception 'El rol institucional Padre o responsable esta inactivo.' using errcode = 'P0001';
    end if;
    if v_rol_base_id is distinct from v_plantilla_id then
      raise exception 'El codigo parent ya existe pero no deriva de la plantilla global parent; requiere revision manual.'
        using errcode = '23505';
    end if;
  end if;

  select u.id, u.auth_user_id, u.activo
    into v_usuario_id, v_auth_user_id, v_usuario_activo
  from public.usuarios u
  where u.persona_id = v_persona_id
  limit 1
  for update;

  if v_usuario_id is null then
    insert into public.usuarios(persona_id, auth_user_id, activo)
    values(v_persona_id, null, true)
    returning id into v_usuario_id;
    v_creo_usuario := true;
  else
    if not v_usuario_activo then
      raise exception 'El usuario asociado al responsable esta inactivo.' using errcode = 'P0001';
    end if;
    if v_auth_user_id is not null then
      raise exception 'El responsable ya tiene una identidad vinculada; administre sus roles como usuario existente.'
        using errcode = 'P0001';
    end if;
  end if;

  select ur.id into v_asignacion_id
  from public.usuarios_roles ur
  where ur.usuario_id = v_usuario_id
    and ur.rol_id = v_rol_id
    and ur.institucion_id = v_institucion_id
    and ur.activo
  limit 1;

  if v_asignacion_id is null then
    insert into public.usuarios_roles(usuario_id, rol_id, institucion_id)
    values(v_usuario_id, v_rol_id, v_institucion_id)
    returning id into v_asignacion_id;
    v_creo_asignacion := true;
  end if;

  select ia.id, ia.rol_id
    into v_invitacion_id, v_invitacion_rol_id
  from public.invitaciones_acceso ia
  where ia.institucion_id = v_institucion_id
    and ia.usuario_id = v_usuario_id
    and ia.estado in ('pendiente', 'enviada', 'aceptada')
  limit 1
  for update;

  if v_invitacion_id is not null and v_invitacion_rol_id <> v_rol_id then
    raise exception 'El responsable ya tiene una invitacion abierta con otro rol; requiere revision manual.'
      using errcode = '23505';
  end if;

  if v_invitacion_id is null then
    insert into public.invitaciones_acceso(
      institucion_id, persona_id, usuario_id, rol_id, correo, correo_normalizado,
      origen, estado, solicitada_por
    ) values (
      v_institucion_id, v_persona_id, v_usuario_id, v_rol_id, v_correo, v_correo,
      'responsable', 'pendiente', v_actor
    ) returning id into v_invitacion_id;
    v_creo_invitacion := true;
  end if;

  if v_creo_rol then
    insert into public.seguridad_auditoria(
      actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
    ) values (
      v_actor, v_institucion_id, 'rol.parent.preparar_responsable', 'rol', v_rol_id,
      jsonb_build_object('rol_base_id', v_plantilla_id, 'responsable_id', p_responsable_id)
    );
  end if;
  if v_creo_usuario then
    insert into public.seguridad_auditoria(
      actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
    ) values (
      v_actor, v_institucion_id, 'usuario.preparar_responsable', 'usuario', v_usuario_id,
      jsonb_build_object('persona_id', v_persona_id, 'responsable_id', p_responsable_id)
    );
  end if;
  if v_creo_asignacion then
    insert into public.seguridad_auditoria(
      actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
    ) values (
      v_actor, v_institucion_id, 'rol_usuario.asignar_responsable', 'usuario_rol', v_asignacion_id,
      jsonb_build_object('usuario_id', v_usuario_id, 'rol_id', v_rol_id, 'responsable_id', p_responsable_id)
    );
  end if;
  if v_creo_invitacion then
    insert into public.seguridad_auditoria(
      actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
    ) values (
      v_actor, v_institucion_id, 'invitacion_acceso.crear_responsable', 'invitacion_acceso', v_invitacion_id,
      jsonb_build_object('usuario_id', v_usuario_id, 'rol_id', v_rol_id, 'correo', v_correo,
                         'origen', 'responsable', 'responsable_id', p_responsable_id)
    );
  end if;

  return jsonb_build_object(
    'responsableId', p_responsable_id,
    'personaId', v_persona_id,
    'usuarioId', v_usuario_id,
    'rolId', v_rol_id,
    'asignacionId', v_asignacion_id,
    'invitacionId', v_invitacion_id,
    'correo', v_correo,
    'estado', 'pendiente',
    'rolCreado', v_creo_rol,
    'usuarioCreado', v_creo_usuario,
    'asignacionCreada', v_creo_asignacion,
    'invitacionCreada', v_creo_invitacion
  );
end
$$;

revoke execute on function public.rpc_preparar_invitacion_responsable(uuid)
  from public, anon, authenticated;
grant execute on function public.rpc_preparar_invitacion_responsable(uuid)
  to service_role;

insert into public.schema_migrations(version, nombre, checksum)
values ('042', 'invitacion_acceso_responsable', null)
on conflict (version) do nothing;

commit;

-- Migracion 040 - alta interna de usuarios e invitaciones de acceso.
-- 046A: prepara identidad interna + rol institucional + invitacion pendiente.
-- No crea auth.users, no envia correo y no vincula OAuth por coincidencia de correo.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '039') then
    raise exception 'La migracion 040 requiere la 039 aplicada previamente.';
  end if;
end
$$;

-- Un usuario interno puede existir antes de que el invitado cree/verifique su
-- identidad en Supabase Auth. El vinculo se completa despues de forma explicita.
alter table public.usuarios alter column auth_user_id drop not null;

create table if not exists public.invitaciones_acceso (
  id uuid primary key default gen_random_uuid(),
  institucion_id uuid not null references public.instituciones(id) on delete restrict,
  persona_id uuid not null references public.personas(id) on delete restrict,
  usuario_id uuid not null references public.usuarios(id) on delete restrict,
  rol_id uuid not null references public.roles(id) on delete restrict,
  correo text not null,
  correo_normalizado text not null,
  origen text not null default 'administracion',
  estado text not null default 'pendiente',
  solicitada_por uuid null references public.usuarios(id) on delete set null,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint ck_invitaciones_acceso_correo check (
    btrim(correo) <> '' and correo_normalizado = lower(btrim(correo))
  ),
  constraint ck_invitaciones_acceso_origen check (
    origen in ('administracion', 'responsable')
  ),
  constraint ck_invitaciones_acceso_estado check (
    estado in ('pendiente', 'enviada', 'aceptada', 'aprobada', 'rechazada', 'expirada', 'revocada')
  )
);

create index if not exists ix_invitaciones_acceso_institucion_estado
  on public.invitaciones_acceso(institucion_id, estado, created_at desc);
create index if not exists ix_invitaciones_acceso_correo
  on public.invitaciones_acceso(correo_normalizado);
create unique index if not exists ux_invitaciones_acceso_abierta_usuario
  on public.invitaciones_acceso(institucion_id, usuario_id)
  where estado in ('pendiente', 'enviada', 'aceptada');

alter table public.invitaciones_acceso enable row level security;
revoke all on table public.invitaciones_acceso from public, anon, authenticated;
grant select, insert, update on table public.invitaciones_acceso to service_role;

create or replace function public.rpc_preparar_invitacion_usuario(
  p_institucion_id uuid,
  p_nombres text,
  p_apellidos text,
  p_correo text,
  p_rol_id uuid,
  p_origen text default 'administracion'
)
returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_actor uuid := public.usuario_actual_id();
  v_correo text := lower(btrim(coalesce(p_correo, '')));
  v_nombres text := btrim(coalesce(p_nombres, ''));
  v_apellidos text := btrim(coalesce(p_apellidos, ''));
  v_origen text := lower(btrim(coalesce(p_origen, '')));
  v_persona_id uuid;
  v_persona_estado text;
  v_usuario_id uuid;
  v_auth_user_id uuid;
  v_usuario_activo boolean;
  v_asignacion_id uuid;
  v_invitacion_id uuid;
  v_invitacion_rol_id uuid;
  v_personas integer;
  v_creo_persona boolean := false;
  v_creo_usuario boolean := false;
  v_creo_asignacion boolean := false;
  v_creo_invitacion boolean := false;
begin
  if v_actor is null then
    raise exception 'Usuario autenticado no vinculado o inactivo.' using errcode = '42501';
  end if;

  if p_institucion_id is null or not exists (
    select 1 from public.instituciones i where i.id = p_institucion_id and i.activo
  ) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;

  if not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.crear', p_institucion_id
  ) or not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.asignar_roles', p_institucion_id
  ) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  if v_nombres = '' or v_apellidos = '' or v_correo = '' then
    raise exception 'Nombres, apellidos y correo son obligatorios.' using errcode = '22023';
  end if;

  if v_origen not in ('administracion', 'responsable') then
    raise exception 'Origen de invitacion no valido.' using errcode = '22023';
  end if;

  if not exists (
    select 1 from public.roles r
    where r.id = p_rol_id
      and r.institucion_id = p_institucion_id
      and r.tipo = 'institucional'
      and r.activo
  ) then
    raise exception 'El rol institucional no existe, esta inactivo o pertenece a otra institucion.'
      using errcode = '23503';
  end if;

  -- Cota de delegacion: el actor no puede entregar permisos que no posee ni
  -- permisos marcados como no delegables.
  if exists (
    select 1
    from public.roles_permisos rp
    join public.permisos p on p.id = rp.permiso_id
    where rp.rol_id = p_rol_id
      and p.estado = 'vigente'
      and (
        p.ambito <> 'institucion'
        or not p.delegable
        or not public.usuario_tiene_permiso_institucional_estricto(p.codigo, p_institucion_id)
      )
  ) then
    raise exception 'El rol contiene permisos que el actor no puede delegar.' using errcode = '42501';
  end if;

  -- Serializa altas concurrentes del mismo correo. Sin este bloqueo, dos
  -- transacciones podrian observar cero coincidencias y crear dos Personas.
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(v_correo, 0));

  select count(*)::integer
    into v_personas
  from public.personas pe
  where lower(btrim(coalesce(pe.correo, ''))) = v_correo;

  if v_personas > 1 then
    raise exception 'El correo coincide con varias personas; requiere revision manual.' using errcode = '21000';
  elsif v_personas = 1 then
    select pe.id, pe.estado
      into v_persona_id, v_persona_estado
    from public.personas pe
    where lower(btrim(coalesce(pe.correo, ''))) = v_correo
    limit 1
    for update;

    if v_persona_estado <> 'activo' then
      raise exception 'La persona asociada al correo esta inactiva.' using errcode = 'P0001';
    end if;
  else
    insert into public.personas(nombres, apellidos, correo)
    values(v_nombres, v_apellidos, v_correo)
    returning id into v_persona_id;
    v_creo_persona := true;
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
      raise exception 'El usuario asociado a la persona esta inactivo.' using errcode = 'P0001';
    end if;
    if v_auth_user_id is not null then
      raise exception 'El usuario ya tiene una identidad vinculada; administre sus roles como usuario existente.'
        using errcode = 'P0001';
    end if;
  end if;

  select ur.id into v_asignacion_id
  from public.usuarios_roles ur
  where ur.usuario_id = v_usuario_id
    and ur.rol_id = p_rol_id
    and ur.institucion_id = p_institucion_id
    and ur.activo
  limit 1;

  if v_asignacion_id is null then
    insert into public.usuarios_roles(usuario_id, rol_id, institucion_id)
    values(v_usuario_id, p_rol_id, p_institucion_id)
    returning id into v_asignacion_id;
    v_creo_asignacion := true;
  end if;

  select ia.id, ia.rol_id
    into v_invitacion_id, v_invitacion_rol_id
  from public.invitaciones_acceso ia
  where ia.institucion_id = p_institucion_id
    and ia.usuario_id = v_usuario_id
    and ia.estado in ('pendiente', 'enviada', 'aceptada')
  limit 1
  for update;

  if v_invitacion_id is not null and v_invitacion_rol_id <> p_rol_id then
    raise exception 'El usuario ya tiene una invitacion abierta con otro rol; revocala antes de cambiarlo.'
      using errcode = '23505';
  end if;

  if v_invitacion_id is null then
    insert into public.invitaciones_acceso(
      institucion_id, persona_id, usuario_id, rol_id, correo, correo_normalizado,
      origen, estado, solicitada_por
    ) values (
      p_institucion_id, v_persona_id, v_usuario_id, p_rol_id, v_correo, v_correo,
      v_origen, 'pendiente', v_actor
    ) returning id into v_invitacion_id;
    v_creo_invitacion := true;
  end if;

  if v_creo_usuario then
    insert into public.seguridad_auditoria(
      actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
    ) values (
      v_actor, p_institucion_id, 'usuario.preparar_invitacion', 'usuario', v_usuario_id,
      jsonb_build_object('persona_id', v_persona_id, 'correo', v_correo, 'origen', v_origen)
    );
  end if;

  if v_creo_asignacion then
    insert into public.seguridad_auditoria(
      actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
    ) values (
      v_actor, p_institucion_id, 'rol_usuario.asignar_invitacion', 'usuario_rol', v_asignacion_id,
      jsonb_build_object('usuario_id', v_usuario_id, 'rol_id', p_rol_id)
    );
  end if;

  if v_creo_invitacion then
    insert into public.seguridad_auditoria(
      actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
    ) values (
      v_actor, p_institucion_id, 'invitacion_acceso.crear', 'invitacion_acceso', v_invitacion_id,
      jsonb_build_object('usuario_id', v_usuario_id, 'rol_id', p_rol_id, 'correo', v_correo, 'origen', v_origen)
    );
  end if;

  return jsonb_build_object(
    'personaId', v_persona_id,
    'usuarioId', v_usuario_id,
    'asignacionId', v_asignacion_id,
    'invitacionId', v_invitacion_id,
    'estado', 'pendiente',
    'correo', v_correo,
    'personaCreada', v_creo_persona,
    'usuarioCreado', v_creo_usuario,
    'asignacionCreada', v_creo_asignacion,
    'invitacionCreada', v_creo_invitacion
  );
end
$$;

revoke all on function public.rpc_preparar_invitacion_usuario(uuid,text,text,text,uuid,text)
  from public, anon;
grant execute on function public.rpc_preparar_invitacion_usuario(uuid,text,text,text,uuid,text)
  to authenticated, service_role;

insert into public.schema_migrations(version, nombre, checksum)
values ('040', 'identidad_invitaciones_acceso', null)
on conflict (version) do nothing;

commit;

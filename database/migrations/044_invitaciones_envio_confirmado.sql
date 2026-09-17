-- Migracion 044 - entrega persistente de invitaciones por correo.
-- Separa la emision del token de la confirmacion real del proveedor de correo.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '043') then
    raise exception 'La migracion 044 requiere la 043 aplicada previamente.';
  end if;
end
$$;

alter table public.invitaciones_acceso
  add column if not exists emision_version bigint not null default 0,
  add column if not exists proveedor_envio text,
  add column if not exists proveedor_mensaje_id text;

do $$
begin
  if not exists (
    select 1 from pg_constraint
    where conname = 'ck_invitaciones_acceso_emision_version'
      and conrelid = 'public.invitaciones_acceso'::regclass
  ) then
    alter table public.invitaciones_acceso
      add constraint ck_invitaciones_acceso_emision_version
      check (emision_version >= 0);
  end if;
end
$$;

create or replace function public.rpc_emitir_invitacion_acceso(
  p_invitacion_id uuid,
  p_token_hash text,
  p_expira_at timestamptz
)
returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_actor uuid := public.usuario_actual_id();
  v_institucion_id uuid;
  v_estado text;
  v_usuario_id uuid;
  v_correo text;
  v_ahora timestamptz := clock_timestamp();
  v_version bigint;
begin
  if v_actor is null then
    raise exception 'Usuario autenticado no vinculado o inactivo.' using errcode = '42501';
  end if;
  if p_invitacion_id is null then
    raise exception 'La invitacion es obligatoria.' using errcode = '22023';
  end if;
  if p_token_hash is null or p_token_hash !~ '^[0-9a-f]{64}$' then
    raise exception 'El hash del token debe ser SHA-256 hexadecimal en minusculas.' using errcode = '22023';
  end if;
  if p_expira_at is null or p_expira_at <= v_ahora + interval '15 minutes' then
    raise exception 'La expiracion debe estar al menos 15 minutos en el futuro.' using errcode = '22023';
  end if;
  if p_expira_at > v_ahora + interval '7 days' then
    raise exception 'La expiracion no puede superar 7 dias.' using errcode = '22023';
  end if;

  select ia.institucion_id, ia.estado, ia.usuario_id, ia.correo
    into v_institucion_id, v_estado, v_usuario_id, v_correo
  from public.invitaciones_acceso ia
  where ia.id = p_invitacion_id
  for update;

  if v_institucion_id is null then
    raise exception 'La invitacion no existe.' using errcode = '23503';
  end if;

  if not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.crear', v_institucion_id
  ) or not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.asignar_roles', v_institucion_id
  ) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  if v_estado not in ('pendiente', 'enviada') then
    raise exception 'La invitacion ya no admite emision o reenvio.' using errcode = 'P0001';
  end if;

  update public.invitaciones_acceso
  set token_hash = p_token_hash,
      token_emitido_at = v_ahora,
      expira_at = p_expira_at,
      enviado_at = null,
      estado = 'pendiente',
      intentos_envio = intentos_envio + 1,
      ultimo_error_envio = null,
      proveedor_envio = null,
      proveedor_mensaje_id = null,
      emision_version = emision_version + 1,
      updated_at = v_ahora
  where id = p_invitacion_id
  returning emision_version into v_version;

  insert into public.seguridad_auditoria(
    actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
  ) values (
    v_actor, v_institucion_id, 'invitacion_acceso.emitir', 'invitacion_acceso', p_invitacion_id,
    jsonb_build_object(
      'usuario_id', v_usuario_id,
      'correo', v_correo,
      'expira_at', p_expira_at,
      'emision_version', v_version,
      'token_persistido', false
    )
  );

  return jsonb_build_object(
    'invitacionId', p_invitacion_id,
    'correo', v_correo,
    'estado', 'pendiente',
    'expiraAt', p_expira_at,
    'intentosEnvio', (
      select intentos_envio from public.invitaciones_acceso where id = p_invitacion_id
    ),
    'emisionVersion', v_version
  );
end
$$;

create or replace function public.rpc_confirmar_envio_invitacion(
  p_invitacion_id uuid,
  p_emision_version bigint,
  p_proveedor text,
  p_mensaje_id text
)
returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_actor uuid := public.usuario_actual_id();
  v_institucion_id uuid;
  v_version bigint;
  v_expira_at timestamptz;
  v_ahora timestamptz := clock_timestamp();
begin
  select institucion_id, emision_version, expira_at
    into v_institucion_id, v_version, v_expira_at
  from public.invitaciones_acceso
  where id = p_invitacion_id
  for update;

  if v_actor is null then
    raise exception 'Usuario autenticado no vinculado o inactivo.' using errcode = '42501';
  end if;
  if v_institucion_id is null then
    raise exception 'La invitacion no existe.' using errcode = '23503';
  end if;
  if not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.crear', v_institucion_id
  ) or not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.asignar_roles', v_institucion_id
  ) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if p_emision_version is null or p_emision_version <> v_version then
    raise exception 'La confirmacion corresponde a una emision obsoleta.' using errcode = 'P0001';
  end if;
  if v_expira_at is null or v_expira_at <= v_ahora then
    raise exception 'La invitacion ya expiro.' using errcode = 'P0001';
  end if;

  update public.invitaciones_acceso
  set estado = 'enviada',
      enviado_at = v_ahora,
      proveedor_envio = nullif(btrim(coalesce(p_proveedor, '')), ''),
      proveedor_mensaje_id = nullif(btrim(coalesce(p_mensaje_id, '')), ''),
      ultimo_error_envio = null,
      updated_at = v_ahora
  where id = p_invitacion_id;

  insert into public.seguridad_auditoria(
    actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
  ) values (
    v_actor, v_institucion_id, 'invitacion_acceso.envio_confirmado', 'invitacion_acceso', p_invitacion_id,
    jsonb_build_object(
      'emision_version', p_emision_version,
      'proveedor', nullif(btrim(coalesce(p_proveedor, '')), ''),
      'mensaje_id', nullif(btrim(coalesce(p_mensaje_id, '')), '')
    )
  );

  return jsonb_build_object(
    'invitacionId', p_invitacion_id,
    'estado', 'enviada',
    'emisionVersion', p_emision_version,
    'enviadoAt', v_ahora
  );
end
$$;

create or replace function public.rpc_registrar_error_envio_invitacion(
  p_invitacion_id uuid,
  p_emision_version bigint,
  p_error text
)
returns void
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_actor uuid := public.usuario_actual_id();
  v_institucion_id uuid;
  v_version bigint;
  v_ahora timestamptz := clock_timestamp();
begin
  select institucion_id, emision_version
    into v_institucion_id, v_version
  from public.invitaciones_acceso
  where id = p_invitacion_id
  for update;

  if v_actor is null then
    raise exception 'Usuario autenticado no vinculado o inactivo.' using errcode = '42501';
  end if;
  if v_institucion_id is null then
    raise exception 'La invitacion no existe.' using errcode = '23503';
  end if;
  if not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.crear', v_institucion_id
  ) or not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.asignar_roles', v_institucion_id
  ) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if p_emision_version is null or p_emision_version <> v_version then
    raise exception 'El error corresponde a una emision obsoleta.' using errcode = 'P0001';
  end if;

  update public.invitaciones_acceso
  set estado = 'pendiente',
      token_hash = null,
      token_emitido_at = null,
      expira_at = null,
      enviado_at = null,
      proveedor_envio = null,
      proveedor_mensaje_id = null,
      ultimo_error_envio = left(coalesce(nullif(btrim(p_error), ''), 'error_envio'), 500),
      updated_at = v_ahora
  where id = p_invitacion_id;

  insert into public.seguridad_auditoria(
    actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
  ) values (
    v_actor, v_institucion_id, 'invitacion_acceso.envio_error', 'invitacion_acceso', p_invitacion_id,
    jsonb_build_object(
      'emision_version', p_emision_version,
      'error', left(coalesce(nullif(btrim(p_error), ''), 'error_envio'), 120)
    )
  );
end
$$;

revoke execute on function public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz)
  from public, anon, authenticated;
revoke execute on function public.rpc_confirmar_envio_invitacion(uuid,bigint,text,text)
  from public, anon, authenticated;
revoke execute on function public.rpc_registrar_error_envio_invitacion(uuid,bigint,text)
  from public, anon, authenticated;

grant execute on function public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz)
  to service_role;
grant execute on function public.rpc_confirmar_envio_invitacion(uuid,bigint,text,text)
  to service_role;
grant execute on function public.rpc_registrar_error_envio_invitacion(uuid,bigint,text)
  to service_role;

insert into public.schema_migrations(version, nombre, checksum)
values ('044', 'invitaciones_envio_confirmado', null)
on conflict (version) do nothing;

commit;

-- Migracion 043 - persistencia segura del ciclo de vida de invitaciones.
-- El token en claro nunca se persiste: la API genera el secreto y entrega solo SHA-256 hex.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '042') then
    raise exception 'La migracion 043 requiere la 042 aplicada previamente.';
  end if;
end
$$;

alter table public.invitaciones_acceso
  add column if not exists token_hash text,
  add column if not exists token_emitido_at timestamptz,
  add column if not exists expira_at timestamptz,
  add column if not exists enviado_at timestamptz,
  add column if not exists aceptado_at timestamptz,
  add column if not exists aprobado_at timestamptz,
  add column if not exists rechazado_at timestamptz,
  add column if not exists revocado_at timestamptz,
  add column if not exists intentos_envio integer not null default 0,
  add column if not exists ultimo_error_envio text;

do $$
begin
  if not exists (
    select 1 from pg_constraint
    where conname = 'ck_invitaciones_acceso_token_hash_sha256'
      and conrelid = 'public.invitaciones_acceso'::regclass
  ) then
    alter table public.invitaciones_acceso
      add constraint ck_invitaciones_acceso_token_hash_sha256
      check (token_hash is null or token_hash ~ '^[0-9a-f]{64}$');
  end if;

  if not exists (
    select 1 from pg_constraint
    where conname = 'ck_invitaciones_acceso_intentos_envio'
      and conrelid = 'public.invitaciones_acceso'::regclass
  ) then
    alter table public.invitaciones_acceso
      add constraint ck_invitaciones_acceso_intentos_envio
      check (intentos_envio >= 0);
  end if;

  if not exists (
    select 1 from pg_constraint
    where conname = 'ck_invitaciones_acceso_expiracion'
      and conrelid = 'public.invitaciones_acceso'::regclass
  ) then
    alter table public.invitaciones_acceso
      add constraint ck_invitaciones_acceso_expiracion
      check (
        expira_at is null
        or token_emitido_at is null
        or expira_at > token_emitido_at
      );
  end if;
end
$$;

create unique index if not exists ux_invitaciones_acceso_token_hash
  on public.invitaciones_acceso(token_hash)
  where token_hash is not null;

create index if not exists ix_invitaciones_acceso_estado_expira
  on public.invitaciones_acceso(estado, expira_at)
  where expira_at is not null;

-- Defensa en profundidad: aunque un GRANT directo se reintroduzca por error,
-- anon/authenticated no pueden leer ni mutar invitaciones mediante Data API.
alter table public.invitaciones_acceso enable row level security;
drop policy if exists invitaciones_acceso_denegar_data_api on public.invitaciones_acceso;
create policy invitaciones_acceso_denegar_data_api
  on public.invitaciones_acceso
  as restrictive
  for all
  to anon, authenticated
  using (false)
  with check (false);

revoke all on table public.invitaciones_acceso from public, anon, authenticated;
grant select, insert, update on table public.invitaciones_acceso to service_role;

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
      enviado_at = v_ahora,
      estado = 'enviada',
      intentos_envio = intentos_envio + 1,
      ultimo_error_envio = null,
      updated_at = v_ahora
  where id = p_invitacion_id;

  insert into public.seguridad_auditoria(
    actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
  ) values (
    v_actor, v_institucion_id, 'invitacion_acceso.emitir', 'invitacion_acceso', p_invitacion_id,
    jsonb_build_object(
      'usuario_id', v_usuario_id,
      'correo', v_correo,
      'expira_at', p_expira_at,
      'token_persistido', false
    )
  );

  return jsonb_build_object(
    'invitacionId', p_invitacion_id,
    'estado', 'enviada',
    'expiraAt', p_expira_at,
    'intentosEnvio', (
      select intentos_envio from public.invitaciones_acceso where id = p_invitacion_id
    )
  );
end
$$;

revoke execute on function public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz)
  from public, anon, authenticated;
grant execute on function public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz)
  to service_role;

insert into public.schema_migrations(version, nombre, checksum)
values ('043', 'invitaciones_persistencia_tokens_rls', null)
on conflict (version) do nothing;

commit;

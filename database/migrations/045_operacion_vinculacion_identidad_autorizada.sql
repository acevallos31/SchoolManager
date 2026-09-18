-- Migracion 045 - solicitud y operacion autorizada de vinculacion de identidad.
-- Completa el ciclo de invitacion sin vincular por correo:
-- 1) una identidad autenticada demuestra posesion del token de invitacion;
-- 2) el backend deriva auth_user_id exclusivamente del claim sub;
-- 3) un operador autorizado aprueba o rechaza la solicitud;
-- 4) la aprobacion reutiliza public.vincular_identidad_usuario (027).
--
-- Esta migracion NO crea identidades, NO asigna roles y NO confia en el correo
-- como prueba de identidad.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '044') then
    raise exception 'La migracion 045 requiere la 044 aplicada previamente.';
  end if;
end
$$;

alter table public.invitaciones_acceso
  add column if not exists auth_user_id_solicitado uuid,
  add column if not exists identidad_solicitada_at timestamptz;

create unique index if not exists ux_invitaciones_acceso_auth_solicitado_abierto
  on public.invitaciones_acceso(auth_user_id_solicitado)
  where auth_user_id_solicitado is not null
    and estado = 'aceptada';

-- Registra que una identidad autenticada posee el token de una invitacion.
-- p_auth_user_id debe ser derivado por el backend desde JWT.sub; nunca se
-- obtiene del body enviado por el navegador.
create or replace function public.rpc_solicitar_vinculacion_invitacion(
  p_token_hash text,
  p_auth_user_id uuid
)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_invitacion_id uuid;
  v_institucion_id uuid;
  v_usuario_id uuid;
  v_estado text;
  v_expira_at timestamptz;
  v_auth_actual uuid;
  v_ahora timestamptz := pg_catalog.clock_timestamp();
begin
  if p_auth_user_id is null then
    raise exception 'La identidad autenticada es obligatoria.' using errcode = '22023';
  end if;

  if p_token_hash is null or p_token_hash !~ '^[0-9a-f]{64}$' then
    raise exception 'El token de invitacion no es valido.' using errcode = '22023';
  end if;

  select ia.id, ia.institucion_id, ia.usuario_id, ia.estado, ia.expira_at
    into v_invitacion_id, v_institucion_id, v_usuario_id, v_estado, v_expira_at
  from public.invitaciones_acceso ia
  where ia.token_hash = p_token_hash
  for update;

  if v_invitacion_id is null then
    raise exception 'La invitacion no existe o el token no es valido.' using errcode = 'P0002';
  end if;

  if v_expira_at is null or v_expira_at <= v_ahora then
    if v_estado in ('pendiente', 'enviada', 'aceptada') then
      update public.invitaciones_acceso
         set estado = 'expirada', updated_at = v_ahora
       where id = v_invitacion_id;
    end if;
    raise exception 'La invitacion expiro.' using errcode = 'P0001';
  end if;

  if v_estado not in ('enviada', 'aceptada') then
    raise exception 'La invitacion no admite aceptacion en su estado actual.' using errcode = 'P0001';
  end if;

  select u.auth_user_id
    into v_auth_actual
  from public.usuarios u
  where u.id = v_usuario_id
  for update;

  if not found then
    raise exception 'El usuario interno de la invitacion no existe.' using errcode = 'P0002';
  end if;

  if v_auth_actual is not null then
    raise exception 'El usuario ya tiene una identidad vinculada.' using errcode = 'P0001';
  end if;

  if exists (
    select 1
    from public.usuarios u
    where u.auth_user_id = p_auth_user_id
      and u.id <> v_usuario_id
  ) then
    raise exception 'La identidad ya esta vinculada a otro usuario.' using errcode = '23505';
  end if;

  if exists (
    select 1
    from public.invitaciones_acceso otra
    where otra.auth_user_id_solicitado = p_auth_user_id
      and otra.estado = 'aceptada'
      and otra.id <> v_invitacion_id
  ) then
    raise exception 'La identidad ya tiene otra solicitud de vinculacion pendiente.' using errcode = '23505';
  end if;

  -- Idempotencia: la misma identidad puede repetir el callback/aceptacion.
  if v_estado = 'aceptada' then
    if (
      select ia.auth_user_id_solicitado
      from public.invitaciones_acceso ia
      where ia.id = v_invitacion_id
    ) = p_auth_user_id then
      return pg_catalog.jsonb_build_object(
        'invitacionId', v_invitacion_id,
        'usuarioId', v_usuario_id,
        'institucionId', v_institucion_id,
        'estado', 'aceptada'
      );
    end if;

    raise exception 'La invitacion ya fue aceptada por otra identidad.' using errcode = '23505';
  end if;

  update public.invitaciones_acceso
     set auth_user_id_solicitado = p_auth_user_id,
         identidad_solicitada_at = v_ahora,
         aceptado_at = v_ahora,
         estado = 'aceptada',
         updated_at = v_ahora
   where id = v_invitacion_id;

  return pg_catalog.jsonb_build_object(
    'invitacionId', v_invitacion_id,
    'usuarioId', v_usuario_id,
    'institucionId', v_institucion_id,
    'estado', 'aceptada'
  );
end
$$;

-- Operacion administrativa. Nunca recibe auth_user_id: lo toma de la
-- invitacion aceptada, que fue poblada por el backend desde JWT.sub.
create or replace function public.rpc_operar_vinculacion_identidad(
  p_invitacion_id uuid,
  p_usuario_id uuid,
  p_institucion_id uuid,
  p_operacion text,
  p_motivo text default null
)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_actor uuid := public.usuario_actual_id();
  v_usuario_id uuid;
  v_auth_user_id uuid;
  v_estado text;
  v_institucion uuid;
  v_operacion text := pg_catalog.lower(pg_catalog.btrim(coalesce(p_operacion, '')));
  v_resultado text;
  v_ahora timestamptz := pg_catalog.clock_timestamp();
begin
  if v_actor is null then
    raise exception 'Usuario autenticado no vinculado o inactivo.' using errcode = '42501';
  end if;

  if p_invitacion_id is null or p_usuario_id is null or p_institucion_id is null then
    raise exception 'Invitacion, usuario e institucion son obligatorios.' using errcode = '22023';
  end if;

  if v_operacion not in ('aprobar', 'rechazar') then
    raise exception 'Operacion de vinculacion no valida.' using errcode = '22023';
  end if;

  if not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.editar', p_institucion_id
  ) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  select ia.usuario_id, ia.auth_user_id_solicitado, ia.estado, ia.institucion_id
    into v_usuario_id, v_auth_user_id, v_estado, v_institucion
  from public.invitaciones_acceso ia
  where ia.id = p_invitacion_id
  for update;

  if v_usuario_id is null then
    raise exception 'La solicitud de vinculacion no existe.' using errcode = 'P0002';
  end if;

  if v_institucion <> p_institucion_id then
    raise exception 'La solicitud pertenece a otra institucion.' using errcode = '42501';
  end if;

  if v_usuario_id <> p_usuario_id then
    raise exception 'La solicitud no corresponde al usuario indicado.' using errcode = '42501';
  end if;

  if v_estado <> 'aceptada' or v_auth_user_id is null then
    raise exception 'La invitacion no tiene una identidad pendiente de aprobacion.' using errcode = 'P0001';
  end if;

  if v_operacion = 'aprobar' then
    v_resultado := public.vincular_identidad_usuario(v_usuario_id, v_auth_user_id);

    update public.invitaciones_acceso
       set estado = 'aprobada',
           aprobado_at = v_ahora,
           updated_at = v_ahora
     where id = p_invitacion_id;

    insert into public.seguridad_auditoria(
      actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
    ) values (
      v_actor, p_institucion_id, 'identidad.vinculacion.aprobar',
      'invitacion_acceso', p_invitacion_id,
      pg_catalog.jsonb_build_object(
        'usuario_id', v_usuario_id,
        'resultado', v_resultado
      )
    );
  else
    update public.invitaciones_acceso
       set estado = 'rechazada',
           rechazado_at = v_ahora,
           updated_at = v_ahora
     where id = p_invitacion_id;

    insert into public.seguridad_auditoria(
      actor_usuario_id, institucion_id, accion, entidad_tipo, entidad_id, detalle
    ) values (
      v_actor, p_institucion_id, 'identidad.vinculacion.rechazar',
      'invitacion_acceso', p_invitacion_id,
      pg_catalog.jsonb_build_object(
        'usuario_id', v_usuario_id,
        'motivo', nullif(pg_catalog.btrim(coalesce(p_motivo, '')), '')
      )
    );
    v_resultado := 'rechazada';
  end if;

  return pg_catalog.jsonb_build_object(
    'invitacionId', p_invitacion_id,
    'usuarioId', v_usuario_id,
    'estado', case when v_operacion = 'aprobar' then 'aprobada' else 'rechazada' end,
    'resultado', v_resultado
  );
end
$$;

revoke all on function public.rpc_solicitar_vinculacion_invitacion(text,uuid)
  from public, anon, authenticated;
grant execute on function public.rpc_solicitar_vinculacion_invitacion(text,uuid)
  to service_role;

revoke all on function public.rpc_operar_vinculacion_identidad(uuid,uuid,uuid,text,text)
  from public, anon, authenticated;
grant execute on function public.rpc_operar_vinculacion_identidad(uuid,uuid,uuid,text,text)
  to service_role;

insert into public.schema_migrations(version, nombre, checksum)
values ('045', 'operacion_vinculacion_identidad_autorizada', null)
on conflict (version) do nothing;

commit;

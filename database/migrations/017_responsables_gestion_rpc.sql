-- Modulo Responsables Fase 018. RPCs de escritura y gestion que cierran la
-- brecha detectada en 018A: los tablas responsables/alumno_responsable ya
-- existian con RLS de solo lectura y grants minimos (revoke all + grant
-- select), pero no habia forma segura de crear/editar/vincular responsables
-- desde cliente. Esta migracion agrega funciones base (security invoker) y
-- wrappers RPC (security definer) replicando el patron vigente
-- (rpc_matricular_alumno, rpc_crear_alumno_*_con_documento).
--
-- Dependencias: 004 (tablas), 007 (RBAC/permisos), 009 (RLS/grant select).
-- No reescribe 004/007/009. Siguiente numero libre tras 016.

begin;

-- Comprobacion de precedencia: requiere la RLS/gestion de identidad de 009.
do $$
begin
  if public.usuario_actual_id() is null then
    raise notice 'funcion usuario_actual_id presente';
  end if;
exception
  when undefined_function then
    raise exception 'Migracion 017 requiere la migracion 009 (seguridad_rls_rpc).';
end;
$$;

-- ---------------------------------------------------------------------------
-- Persona: crear o reutilizar identidad por documento (unicidad por
-- tipo_identificacion + pais_emisor + numero normalizado, indice 003).
-- ---------------------------------------------------------------------------
create or replace function public.crear_o_reutilizar_persona_con_documento(
  p_nombres text,
  p_apellidos text,
  p_tipo_identificacion text,
  p_numero_identificacion text,
  p_telefono text default null,
  p_correo text default null,
  p_pais_emisor text default null
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare
  v_documento_normalizado text;
  v_persona_id uuid;
begin
  if btrim(coalesce(p_nombres, '')) = '' or btrim(coalesce(p_apellidos, '')) = '' then
    raise exception 'Nombres y apellidos son obligatorios.' using errcode = '22023';
  end if;
  if btrim(coalesce(p_tipo_identificacion, '')) = ''
     or btrim(coalesce(p_numero_identificacion, '')) = '' then
    raise exception 'Tipo y numero de identificacion son obligatorios.' using errcode = '22023';
  end if;

  v_documento_normalizado := lower(regexp_replace(
    btrim(p_numero_identificacion), '[^[:alnum:]]', '', 'g'));
  if v_documento_normalizado = '' then
    raise exception 'El numero de identificacion no es valido.' using errcode = '22023';
  end if;

  -- Reutiliza la persona existente por documento en lugar de duplicarla.
  select id into v_persona_id
  from public.personas
  where lower(tipo_identificacion) = lower(btrim(p_tipo_identificacion))
    and lower(coalesce(pais_emisor, '')) = lower(coalesce(p_pais_emisor, ''))
    and numero_identificacion_normalizado = v_documento_normalizado
    and estado = 'activo'
  limit 1;

  if v_persona_id is null then
    insert into public.personas (
      nombres, apellidos, tipo_identificacion, numero_identificacion,
      numero_identificacion_normalizado, telefono, correo, pais_emisor
    ) values (
      btrim(p_nombres), btrim(p_apellidos), lower(btrim(p_tipo_identificacion)),
      btrim(p_numero_identificacion), v_documento_normalizado,
      nullif(btrim(coalesce(p_telefono, '')), ''), nullif(btrim(coalesce(p_correo, '')), ''),
      nullif(btrim(coalesce(p_pais_emisor, '')), '')
    ) returning id into v_persona_id;
  end if;

  return v_persona_id;
end;
$$;

-- ---------------------------------------------------------------------------
-- Responsable: crear a partir de una Persona en una Institucion.
-- Invariante: uq_responsables_persona_institucion (permite solo un
-- responsable por persona+institucion).
-- ---------------------------------------------------------------------------
create or replace function public.crear_responsable(
  p_persona_id uuid,
  p_institucion_id uuid
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare v_responsable_id uuid;
begin
  if not exists (select 1 from public.personas where id = p_persona_id and estado = 'activo') then
    raise exception 'La persona no existe o esta inactiva.' using errcode = '23503';
  end if;
  if not exists (select 1 from public.instituciones where id = p_institucion_id and activo) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;
  insert into public.responsables (persona_id, institucion_id)
  values (p_persona_id, p_institucion_id)
  returning id into v_responsable_id;
  return v_responsable_id;
end;
$$;

-- Wrapper RPC: crear responsable (persona + responsable) con documento.
create or replace function public.rpc_crear_responsable_con_documento(
  p_institucion_id uuid,
  p_nombres text,
  p_apellidos text,
  p_tipo_identificacion text,
  p_numero_identificacion text,
  p_telefono text default null,
  p_correo text default null
)
returns uuid
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_persona_id uuid;
begin
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.responsables.crear', p_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v_persona_id := public.crear_o_reutilizar_persona_con_documento(
    p_nombres, p_apellidos, p_tipo_identificacion, p_numero_identificacion,
    p_telefono, p_correo, null);
  return public.crear_responsable(v_persona_id, p_institucion_id);
end;
$$;

-- Wrapper RPC: crear responsable para una persona ya existente (sin duplicar).
create or replace function public.rpc_crear_responsable_para_persona(
  p_persona_id uuid,
  p_institucion_id uuid
)
returns uuid
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
begin
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.responsables.crear', p_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  return public.crear_responsable(p_persona_id, p_institucion_id);
end;
$$;

-- ---------------------------------------------------------------------------
-- Responsable: editar datos permitidos de la Persona vinculada.
-- No se permite cambiar persona/institucion del responsable (mover identidad).
-- ---------------------------------------------------------------------------
create or replace function public.editar_datos_persona(
  p_persona_id uuid,
  p_nombres text,
  p_apellidos text,
  p_telefono text default null,
  p_correo text default null
)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
begin
  if btrim(coalesce(p_nombres, '')) = '' or btrim(coalesce(p_apellidos, '')) = '' then
    raise exception 'Nombres y apellidos son obligatorios.' using errcode = '22023';
  end if;
  if not exists (select 1 from public.personas where id = p_persona_id and estado = 'activo') then
    raise exception 'La persona no existe o esta inactiva.' using errcode = '23503';
  end if;
  update public.personas
  set nombres = btrim(p_nombres),
      apellidos = btrim(p_apellidos),
      telefono = nullif(btrim(coalesce(p_telefono, '')), ''),
      correo = nullif(btrim(coalesce(p_correo, '')), ''),
      updated_at = now()
  where id = p_persona_id;
end;
$$;

create or replace function public.rpc_editar_responsable(
  p_responsable_id uuid,
  p_nombres text,
  p_apellidos text,
  p_telefono text default null,
  p_correo text default null
)
returns void
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_persona_id uuid; v_institucion_id uuid;
begin
  select persona_id, institucion_id into v_persona_id, v_institucion_id
  from public.responsables where id = p_responsable_id and estado = 'activo';
  if not found then raise exception 'El responsable no existe o esta inactivo.' using errcode = 'P0002'; end if;
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.responsables.editar', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  perform public.editar_datos_persona(v_persona_id, p_nombres, p_apellidos, p_telefono, p_correo);
  update public.responsables set updated_at = now() where id = p_responsable_id;
end;
$$;

-- ---------------------------------------------------------------------------
-- Responsable: inactivar/reactivar (soft, sin DELETE fisico).
-- ---------------------------------------------------------------------------
create or replace function public.inactivar_responsable(
  p_responsable_id uuid,
  p_motivo text
)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
begin
  if p_motivo is null or btrim(p_motivo) = '' then
    raise exception 'El motivo es obligatorio.' using errcode = '22023';
  end if;
  if not exists (select 1 from public.responsables where id = p_responsable_id and estado = 'activo') then
    raise exception 'El responsable no existe o esta inactivo.' using errcode = 'P0002';
  end if;
  update public.responsables
  set estado = 'inactivo',
      fecha_desactivacion = now(),
      motivo_desactivacion = btrim(p_motivo),
      updated_at = now()
  where id = p_responsable_id;
  -- Los vinculos activos del responsable quedan inactivos (coherencia).
  update public.alumno_responsable
  set estado = 'inactivo',
      fecha_desactivacion = now(),
      motivo_desactivacion = 'Responsable inactivado: ' || btrim(p_motivo),
      updated_at = now()
  where responsable_id = p_responsable_id and estado = 'activo';
end;
$$;

create or replace function public.rpc_inactivar_responsable(p_responsable_id uuid, p_motivo text)
returns void
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid;
begin
  select institucion_id into v_institucion_id
  from public.responsables where id = p_responsable_id and estado = 'activo';
  if not found then raise exception 'El responsable no existe o esta inactivo.' using errcode = 'P0002'; end if;
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.responsables.editar', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  perform public.inactivar_responsable(p_responsable_id, p_motivo);
end;
$$;

create or replace function public.reactivar_responsable(p_responsable_id uuid)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
begin
  if not exists (select 1 from public.responsables where id = p_responsable_id and estado = 'inactivo') then
    raise exception 'El responsable no existe o esta activo.' using errcode = 'P0002';
  end if;
  update public.responsables
  set estado = 'activo',
      fecha_desactivacion = null,
      motivo_desactivacion = null,
      updated_at = now()
  where id = p_responsable_id;
end;
$$;

create or replace function public.rpc_reactivar_responsable(p_responsable_id uuid)
returns void
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid;
begin
  select institucion_id into v_institucion_id
  from public.responsables where id = p_responsable_id and estado = 'inactivo';
  if not found then raise exception 'El responsable no existe o esta activo.' using errcode = 'P0002'; end if;
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.responsables.editar', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  perform public.reactivar_responsable(p_responsable_id);
end;
$$;

-- ---------------------------------------------------------------------------
-- Vinculo alumno_responsable: vincular, editar, marcar principal,
-- desactivar/reactivar. Impide cruce de instituciones.
-- Invariante de un principal activo por alumno => indice parcial ux_*_principal_activo.
-- ---------------------------------------------------------------------------
create or replace function public.vincular_alumno_responsable(
  p_alumno_id uuid,
  p_responsable_id uuid,
  p_parentesco text default null,
  p_es_principal boolean default false,
  p_acceso_financiero boolean default false
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare
  v_alumno_institucion uuid;
  v_responsable_institucion uuid;
  v_vinculo_id uuid;
begin
  -- Bloquear la fila del alumno: serializa las operaciones de vinculacion sobre
  -- el mismo alumno y preserva el invariante "un unico principal activo" ante
  -- dos intentos simultaneos de establecer responsables distintos como principal.
  select institucion_id into v_alumno_institucion
  from public.alumnos where id = p_alumno_id and estado = 'activo'
  for update;
  if v_alumno_institucion is null then
    raise exception 'El alumno no existe o esta inactivo.' using errcode = '23503';
  end if;
  select institucion_id into v_responsable_institucion
  from public.responsables where id = p_responsable_id and estado = 'activo';
  if v_responsable_institucion is null then
    raise exception 'El responsable no existe o esta inactivo.' using errcode = '23503';
  end if;
  -- No cruzar instituciones.
  if v_alumno_institucion <> v_responsable_institucion then
    raise exception 'Alumno y responsable deben pertenecer a la misma institucion.' using errcode = '23514';
  end if;

  -- Si existe vinculo inactivo (historico), reactivarlo en vez de duplicar.
  select id into v_vinculo_id
  from public.alumno_responsable
  where alumno_id = p_alumno_id and responsable_id = p_responsable_id and estado = 'inactivo'
  limit 1;

  if v_vinculo_id is not null then
    -- Si se reclama el rol de principal, liberarlo PRIMERO en los demas vinculos
    -- activos del alumno; asi el UPDATE de reactivacion de abajo no viola
    -- ux_alumno_responsable_principal_activo (que se evalua por sentencia).
    if p_es_principal then
      update public.alumno_responsable
      set es_principal = false, updated_at = now()
      where alumno_id = p_alumno_id and estado = 'activo' and id <> v_vinculo_id;
    end if;
    update public.alumno_responsable
    set parentesco = nullif(btrim(coalesce(p_parentesco, '')), ''),
        es_principal = p_es_principal,
        acceso_financiero = p_acceso_financiero,
        estado = 'activo',
        fecha_desactivacion = null,
        motivo_desactivacion = null,
        updated_at = now()
    where id = v_vinculo_id;
    return v_vinculo_id;
  end if;

  -- Si se reclama el rol de principal, liberarlo PRIMERO en los vinculos activos
  -- del alumno antes de insertar (mismo invariante de principal unico).
  if p_es_principal then
    update public.alumno_responsable
    set es_principal = false, updated_at = now()
    where alumno_id = p_alumno_id and estado = 'activo';
  end if;

  insert into public.alumno_responsable (
    alumno_id, responsable_id, parentesco, es_principal, acceso_financiero
  ) values (
    p_alumno_id, p_responsable_id,
    nullif(btrim(coalesce(p_parentesco, '')), ''), p_es_principal, p_acceso_financiero
  ) returning id into v_vinculo_id;

  return v_vinculo_id;
end;
$$;

create or replace function public.rpc_vincular_alumno_responsable(
  p_alumno_id uuid,
  p_responsable_id uuid,
  p_parentesco text default null,
  p_es_principal boolean default false,
  p_acceso_financiero boolean default false
)
returns uuid
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid;
begin
  select institucion_id into v_institucion_id from public.alumnos where id = p_alumno_id;
  if v_institucion_id is null then raise exception 'El alumno no existe.' using errcode = '23503'; end if;
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.responsables.editar', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  return public.vincular_alumno_responsable(
    p_alumno_id, p_responsable_id, p_parentesco, p_es_principal, p_acceso_financiero);
end;
$$;

-- Editar datos de un vinculo existente (parentesco, acceso financiero, principal).
create or replace function public.editar_vinculo_responsable(
  p_vinculo_id uuid,
  p_parentesco text default null,
  p_es_principal boolean default null,
  p_acceso_financiero boolean default null
)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare v_alumno_id uuid;
begin
  -- Bloquear la fila del alumno para serializar ediciones de vinculos del mismo
  -- alumno y preservar el invariante "un unico principal activo" bajo concurrencia.
  select ar.alumno_id into v_alumno_id
  from public.alumno_responsable ar
  join public.alumnos a on a.id = ar.alumno_id
  where ar.id = p_vinculo_id and ar.estado = 'activo'
  for update of a;
  if v_alumno_id is null then
    raise exception 'El vinculo no existe o esta inactivo.' using errcode = 'P0002';
  end if;
  update public.alumno_responsable
  set parentesco = coalesce(nullif(btrim(coalesce(p_parentesco, '')), ''), btrim(coalesce(parentesco, ''))),
      acceso_financiero = coalesce(p_acceso_financiero, acceso_financiero),
      updated_at = now()
  where id = p_vinculo_id;

  if p_es_principal then
    update public.alumno_responsable
    set es_principal = false, updated_at = now()
    where alumno_id = v_alumno_id and estado = 'activo' and id <> p_vinculo_id;
    update public.alumno_responsable set es_principal = true, updated_at = now()
    where id = p_vinculo_id;
  elsif p_es_principal is not null and not p_es_principal then
    update public.alumno_responsable set es_principal = false, updated_at = now()
    where id = p_vinculo_id;
  end if;
end;
$$;

create or replace function public.rpc_editar_vinculo_responsable(
  p_vinculo_id uuid,
  p_parentesco text default null,
  p_es_principal boolean default null,
  p_acceso_financiero boolean default null
)
returns void
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid;
begin
  select r.institucion_id into v_institucion_id
  from public.alumno_responsable ar
  join public.responsables r on r.id = ar.responsable_id
  where ar.id = p_vinculo_id and ar.estado = 'activo';
  if v_institucion_id is null then raise exception 'El vinculo no existe o esta inactivo.' using errcode = 'P0002'; end if;
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.responsables.editar', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  perform public.editar_vinculo_responsable(p_vinculo_id, p_parentesco, p_es_principal, p_acceso_financiero);
end;
$$;

-- Desactivar vinculo (soft).
create or replace function public.desactivar_vinculo_responsable(p_vinculo_id uuid, p_motivo text)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
begin
  if p_motivo is null or btrim(p_motivo) = '' then
    raise exception 'El motivo es obligatorio.' using errcode = '22023';
  end if;
  if not exists (select 1 from public.alumno_responsable where id = p_vinculo_id and estado = 'activo') then
    raise exception 'El vinculo no existe o esta inactivo.' using errcode = 'P0002';
  end if;
  update public.alumno_responsable
  set estado = 'inactivo',
      es_principal = false,
      fecha_desactivacion = now(),
      motivo_desactivacion = btrim(p_motivo),
      updated_at = now()
  where id = p_vinculo_id;
end;
$$;

create or replace function public.rpc_desactivar_vinculo_responsable(p_vinculo_id uuid, p_motivo text)
returns void
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid;
begin
  select r.institucion_id into v_institucion_id
  from public.alumno_responsable ar
  join public.responsables r on r.id = ar.responsable_id
  where ar.id = p_vinculo_id and ar.estado = 'activo';
  if v_institucion_id is null then raise exception 'El vinculo no existe o esta inactivo.' using errcode = 'P0002'; end if;
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.responsables.editar', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  perform public.desactivar_vinculo_responsable(p_vinculo_id, p_motivo);
end;
$$;

-- Reactivar vinculo (restaurar historico sin crear duplicado).
create or replace function public.reactivar_vinculo_responsable(p_vinculo_id uuid)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
begin
  if not exists (select 1 from public.alumno_responsable where id = p_vinculo_id and estado = 'inactivo') then
    raise exception 'El vinculo no existe o esta activo.' using errcode = 'P0002';
  end if;
  if not exists (
    select 1 from public.alumno_responsable ar
    join public.responsables r on r.id = ar.responsable_id
    where ar.id = p_vinculo_id and r.estado = 'activo'
  ) then
    raise exception 'El responsable vinculado esta inactivo.' using errcode = '22023';
  end if;
  update public.alumno_responsable
  set estado = 'activo',
      fecha_desactivacion = null,
      motivo_desactivacion = null,
      updated_at = now()
  where id = p_vinculo_id;
end;
$$;

create or replace function public.rpc_reactivar_vinculo_responsable(p_vinculo_id uuid)
returns void
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid;
begin
  select r.institucion_id into v_institucion_id
  from public.alumno_responsable ar
  join public.responsables r on r.id = ar.responsable_id
  where ar.id = p_vinculo_id and ar.estado = 'inactivo';
  if v_institucion_id is null then raise exception 'El vinculo no existe o esta activo.' using errcode = 'P0002'; end if;
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.responsables.editar', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  perform public.reactivar_vinculo_responsable(p_vinculo_id);
end;
$$;

-- ---------------------------------------------------------------------------
-- Grants minimos: solo los RPC de escritura quedan accesibles a authenticated.
-- Las funciones base (invoker) se reservan a service_role / llamadas internas.
-- ---------------------------------------------------------------------------
revoke execute on function public.crear_o_reutilizar_persona_con_documento(
  text, text, text, text, text, text, text) from public, anon, authenticated;
revoke execute on function public.crear_responsable(uuid, uuid) from public, anon, authenticated;
revoke execute on function public.editar_datos_persona(uuid, text, text, text, text) from public, anon, authenticated;
revoke execute on function public.inactivar_responsable(uuid, text) from public, anon, authenticated;
revoke execute on function public.reactivar_responsable(uuid) from public, anon, authenticated;
revoke execute on function public.vincular_alumno_responsable(uuid, uuid, text, boolean, boolean) from public, anon, authenticated;
revoke execute on function public.editar_vinculo_responsable(uuid, text, boolean, boolean) from public, anon, authenticated;
revoke execute on function public.desactivar_vinculo_responsable(uuid, text) from public, anon, authenticated;
revoke execute on function public.reactivar_vinculo_responsable(uuid) from public, anon, authenticated;

grant execute on function public.rpc_crear_responsable_con_documento(
  uuid, text, text, text, text, text, text) to authenticated;
grant execute on function public.rpc_crear_responsable_para_persona(uuid, uuid) to authenticated;
grant execute on function public.rpc_editar_responsable(uuid, text, text, text, text) to authenticated;
grant execute on function public.rpc_inactivar_responsable(uuid, text) to authenticated;
grant execute on function public.rpc_reactivar_responsable(uuid) to authenticated;
grant execute on function public.rpc_vincular_alumno_responsable(
  uuid, uuid, text, boolean, boolean) to authenticated;
grant execute on function public.rpc_editar_vinculo_responsable(uuid, text, boolean, boolean) to authenticated;
grant execute on function public.rpc_desactivar_vinculo_responsable(uuid, text) to authenticated;
grant execute on function public.rpc_reactivar_vinculo_responsable(uuid) to authenticated;

grant all privileges on function public.crear_o_reutilizar_persona_con_documento(
  text, text, text, text, text, text, text) to service_role;
grant all privileges on function public.crear_responsable(uuid, uuid) to service_role;
grant all privileges on function public.editar_datos_persona(uuid, text, text, text, text) to service_role;
grant all privileges on function public.inactivar_responsable(uuid, text) to service_role;
grant all privileges on function public.reactivar_responsable(uuid) to service_role;
grant all privileges on function public.vincular_alumno_responsable(uuid, uuid, text, boolean, boolean) to service_role;
grant all privileges on function public.editar_vinculo_responsable(uuid, text, boolean, boolean) to service_role;
grant all privileges on function public.desactivar_vinculo_responsable(uuid, text) to service_role;
grant all privileges on function public.reactivar_vinculo_responsable(uuid) to service_role;
grant execute on function public.rpc_crear_responsable_con_documento(
  uuid, text, text, text, text, text, text) to service_role;
grant execute on function public.rpc_crear_responsable_para_persona(uuid, uuid) to service_role;
grant execute on function public.rpc_editar_responsable(uuid, text, text, text, text) to service_role;
grant execute on function public.rpc_inactivar_responsable(uuid, text) to service_role;
grant execute on function public.rpc_reactivar_responsable(uuid) to service_role;
grant execute on function public.rpc_vincular_alumno_responsable(
  uuid, uuid, text, boolean, boolean) to service_role;
grant execute on function public.rpc_editar_vinculo_responsable(uuid, text, boolean, boolean) to service_role;
grant execute on function public.rpc_desactivar_vinculo_responsable(uuid, text) to service_role;
grant execute on function public.rpc_reactivar_vinculo_responsable(uuid) to service_role;

insert into public.schema_migrations (version, nombre, checksum)
values ('017', 'responsables_gestion_rpc', null)
on conflict (version) do nothing;

commit;
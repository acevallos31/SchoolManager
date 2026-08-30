-- Modulo Alumnos Fase 1C. Extiende la creacion atomica para guardar el
-- documento en Persona, sin reutilizar RNE como identidad civil.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '010') then
    raise exception 'Migracion 011 requiere la consolidacion Fase 1C (010).';
  end if;
end;
$$;

create or replace function public.crear_alumno_nueva_persona_con_documento(
  p_institucion_id uuid,
  p_nombres text,
  p_apellidos text,
  p_tipo_identificacion text,
  p_numero_identificacion text,
  p_fecha_nacimiento date default null,
  p_rne text default null,
  p_codigo_interno text default null
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare
  v_persona_id uuid;
  v_alumno_id uuid;
  v_documento_normalizado text;
begin
  if not exists (select 1 from public.instituciones where id = p_institucion_id and activo) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;
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

  insert into public.personas (
    nombres, apellidos, tipo_identificacion, numero_identificacion,
    numero_identificacion_normalizado
  ) values (
    btrim(p_nombres), btrim(p_apellidos), lower(btrim(p_tipo_identificacion)),
    btrim(p_numero_identificacion), v_documento_normalizado
  ) returning id into v_persona_id;

  insert into public.alumnos (
    persona_id, institucion_id, fecha_nacimiento, rne, codigo_interno
  ) values (
    v_persona_id, p_institucion_id, p_fecha_nacimiento,
    nullif(btrim(p_rne), ''), nullif(btrim(p_codigo_interno), '')
  ) returning id into v_alumno_id;

  return v_alumno_id;
end;
$$;

create or replace function public.rpc_crear_alumno_nueva_persona_con_documento(
  p_institucion_id uuid,
  p_nombres text,
  p_apellidos text,
  p_tipo_identificacion text,
  p_numero_identificacion text,
  p_fecha_nacimiento date default null,
  p_rne text default null,
  p_codigo_interno text default null
)
returns uuid
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
begin
  if public.usuario_actual_id() is null
     or not public.usuario_tiene_permiso_actual(
       'academico.alumnos.crear', p_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  return public.crear_alumno_nueva_persona_con_documento(
    p_institucion_id, p_nombres, p_apellidos, p_tipo_identificacion,
    p_numero_identificacion, p_fecha_nacimiento, p_rne, p_codigo_interno);
end;
$$;

revoke execute on function public.crear_alumno_nueva_persona_con_documento(
  uuid, text, text, text, text, date, text, text) from public, anon, authenticated;
revoke execute on function public.rpc_crear_alumno_nueva_persona_con_documento(
  uuid, text, text, text, text, date, text, text) from public, anon;
grant execute on function public.rpc_crear_alumno_nueva_persona_con_documento(
  uuid, text, text, text, text, date, text, text) to authenticated;
grant execute on function public.crear_alumno_nueva_persona_con_documento(
  uuid, text, text, text, text, date, text, text) to service_role;
grant execute on function public.rpc_crear_alumno_nueva_persona_con_documento(
  uuid, text, text, text, text, date, text, text) to service_role;

insert into public.schema_migrations (version, nombre, checksum)
values ('011', 'extender_creacion_alumno_con_documento', null)
on conflict (version) do nothing;

commit;

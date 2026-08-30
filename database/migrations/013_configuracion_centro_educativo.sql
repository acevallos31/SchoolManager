-- Configuracion segura de instituciones y reglas de identificacion de alumnos.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '012') then
    raise exception 'Migracion 013 requiere la migracion 012.';
  end if;
end;
$$;

create or replace function public.rpc_obtener_configuracion_institucion(
  p_institucion_id uuid default null
)
returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_multi boolean;
  v_institucion_id uuid;
  v_institucion public.instituciones%rowtype;
  v_identificadores public.configuracion_identificadores%rowtype;
  v_activas integer;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  select multiples_instituciones into v_multi
  from public.configuracion_implementacion where id = 1;
  if not found then
    raise exception 'CONFIGURATION_REQUIRED: falta la configuracion de implementacion.'
      using errcode = 'SM001';
  end if;

  if v_multi then
    if p_institucion_id is null then
      raise exception 'INSTITUTION_CONTEXT_REQUIRED: seleccione una institucion.'
        using errcode = 'SM003';
    end if;
    v_institucion_id := p_institucion_id;
  else
    select count(*)::integer into v_activas
    from public.instituciones where activo;
    if v_activas > 1 then
      raise exception 'MULTIPLE_INSTITUTIONS_IN_SINGLE_MODE: hay varias instituciones activas.'
        using errcode = 'SM002';
    end if;
    if v_activas = 0 then
      if not (
        public.usuario_tiene_permiso_actual('configuracion.instituciones.ver', null)
        or public.usuario_tiene_permiso_actual('configuracion.instituciones.editar', null)
      ) then
        raise exception 'Permiso denegado.' using errcode = '42501';
      end if;
      return jsonb_build_object(
        'multiplesInstituciones', false,
        'institucion', null,
        'identificadores', null
      );
    end if;
    select id into v_institucion_id from public.instituciones where activo;
  end if;

  if not (
    public.usuario_tiene_permiso_actual('configuracion.instituciones.ver', v_institucion_id)
    or public.usuario_tiene_permiso_actual('configuracion.instituciones.editar', v_institucion_id)
  ) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  select * into v_institucion
  from public.instituciones
  where id = v_institucion_id and activo;
  if not found then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = 'P0002';
  end if;

  select * into v_identificadores
  from public.configuracion_identificadores
  where institucion_id = v_institucion_id;

  return jsonb_build_object(
    'multiplesInstituciones', v_multi,
    'institucion', jsonb_build_object(
      'id', v_institucion.id,
      'nombre', v_institucion.nombre,
      'nombreCorto', v_institucion.nombre_corto,
      'direccion', v_institucion.direccion,
      'telefono', v_institucion.telefono,
      'correo', v_institucion.correo,
      'logoUrl', v_institucion.logo_url
    ),
    'identificadores', jsonb_build_object(
      'rneRequerido', coalesce(v_identificadores.rne_requerido, false),
      'identificacionCivilRequerida', coalesce(v_identificadores.identificacion_civil_requerida, false),
      'codigoInternoRequerido', coalesce(v_identificadores.codigo_interno_requerido, false),
      'tiposIdentificacionPermitidos', coalesce(
        v_identificadores.tipos_identificacion_permitidos,
        array['identidad', 'pasaporte', 'otro']::text[]
      )
    )
  );
end;
$$;

create or replace function public.rpc_crear_institucion(
  p_nombre text,
  p_nombre_corto text default null,
  p_direccion text default null,
  p_telefono text default null,
  p_correo text default null,
  p_logo_url text default null,
  p_rne_requerido boolean default false,
  p_identificacion_civil_requerida boolean default false,
  p_codigo_interno_requerido boolean default false,
  p_tipos_identificacion_permitidos text[] default array['identidad', 'pasaporte', 'otro']::text[]
)
returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_multi boolean;
  v_institucion_id uuid;
  v_tipos text[];
begin
  if public.usuario_actual_id() is null
     or not public.usuario_tiene_permiso_actual('configuracion.instituciones.editar', null) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if btrim(coalesce(p_nombre, '')) = '' then
    raise exception 'El nombre del centro educativo es obligatorio.' using errcode = '22023';
  end if;
  if nullif(btrim(coalesce(p_correo, '')), '') is not null
     and btrim(p_correo) !~* '^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$' then
    raise exception 'El correo no tiene un formato valido.' using errcode = '22023';
  end if;

  select multiples_instituciones into v_multi
  from public.configuracion_implementacion where id = 1;
  if not found then
    raise exception 'CONFIGURATION_REQUIRED: falta la configuracion de implementacion.' using errcode = 'SM001';
  end if;
  if not v_multi and exists (select 1 from public.instituciones where activo) then
    raise exception 'ACTIVE_INSTITUTION_ALREADY_EXISTS: el modo single ya tiene una institucion activa.'
      using errcode = 'SM004';
  end if;

  select coalesce(array_agg(distinct lower(btrim(tipo)) order by lower(btrim(tipo))), '{}')
  into v_tipos
  from unnest(coalesce(p_tipos_identificacion_permitidos, '{}'::text[])) tipo
  where btrim(tipo) <> '';

  insert into public.instituciones (
    nombre, nombre_corto, direccion, telefono, correo, logo_url
  ) values (
    btrim(p_nombre), nullif(btrim(p_nombre_corto), ''), nullif(btrim(p_direccion), ''),
    nullif(btrim(p_telefono), ''), nullif(lower(btrim(p_correo)), ''),
    nullif(btrim(p_logo_url), '')
  ) returning id into v_institucion_id;

  insert into public.configuracion_identificadores (
    institucion_id, rne_requerido, identificacion_civil_requerida,
    codigo_interno_requerido, tipos_identificacion_permitidos
  ) values (
    v_institucion_id, p_rne_requerido, p_identificacion_civil_requerida,
    p_codigo_interno_requerido, v_tipos
  );

  return public.rpc_obtener_configuracion_institucion(v_institucion_id);
end;
$$;

create or replace function public.rpc_actualizar_institucion(
  p_institucion_id uuid,
  p_nombre text,
  p_nombre_corto text default null,
  p_direccion text default null,
  p_telefono text default null,
  p_correo text default null,
  p_logo_url text default null,
  p_rne_requerido boolean default false,
  p_identificacion_civil_requerida boolean default false,
  p_codigo_interno_requerido boolean default false,
  p_tipos_identificacion_permitidos text[] default array['identidad', 'pasaporte', 'otro']::text[]
)
returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_tipos text[];
begin
  if public.usuario_actual_id() is null
     or not public.usuario_tiene_permiso_actual(
       'configuracion.instituciones.editar', p_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if btrim(coalesce(p_nombre, '')) = '' then
    raise exception 'El nombre del centro educativo es obligatorio.' using errcode = '22023';
  end if;
  if nullif(btrim(coalesce(p_correo, '')), '') is not null
     and btrim(p_correo) !~* '^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$' then
    raise exception 'El correo no tiene un formato valido.' using errcode = '22023';
  end if;
  if not exists (select 1 from public.instituciones where id = p_institucion_id and activo) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = 'P0002';
  end if;

  select coalesce(array_agg(distinct lower(btrim(tipo)) order by lower(btrim(tipo))), '{}')
  into v_tipos
  from unnest(coalesce(p_tipos_identificacion_permitidos, '{}'::text[])) tipo
  where btrim(tipo) <> '';

  update public.instituciones
  set nombre = btrim(p_nombre),
      nombre_corto = nullif(btrim(p_nombre_corto), ''),
      direccion = nullif(btrim(p_direccion), ''),
      telefono = nullif(btrim(p_telefono), ''),
      correo = nullif(lower(btrim(p_correo)), ''),
      logo_url = nullif(btrim(p_logo_url), ''),
      updated_at = now()
  where id = p_institucion_id;

  insert into public.configuracion_identificadores (
    institucion_id, rne_requerido, identificacion_civil_requerida,
    codigo_interno_requerido, tipos_identificacion_permitidos
  ) values (
    p_institucion_id, p_rne_requerido, p_identificacion_civil_requerida,
    p_codigo_interno_requerido, v_tipos
  )
  on conflict (institucion_id) do update
  set rne_requerido = excluded.rne_requerido,
      identificacion_civil_requerida = excluded.identificacion_civil_requerida,
      codigo_interno_requerido = excluded.codigo_interno_requerido,
      tipos_identificacion_permitidos = excluded.tipos_identificacion_permitidos,
      updated_at = now();

  return public.rpc_obtener_configuracion_institucion(p_institucion_id);
end;
$$;

alter table public.instituciones enable row level security;
alter table public.configuracion_identificadores enable row level security;

revoke all privileges on public.instituciones, public.configuracion_identificadores
  from public, anon, authenticated;
grant all privileges on public.instituciones, public.configuracion_identificadores to service_role;

revoke execute on function public.rpc_obtener_configuracion_institucion(uuid) from public, anon;
revoke execute on function public.rpc_crear_institucion(
  text, text, text, text, text, text, boolean, boolean, boolean, text[]) from public, anon;
revoke execute on function public.rpc_actualizar_institucion(
  uuid, text, text, text, text, text, text, boolean, boolean, boolean, text[]) from public, anon;
grant execute on function public.rpc_obtener_configuracion_institucion(uuid) to authenticated;
grant execute on function public.rpc_crear_institucion(
  text, text, text, text, text, text, boolean, boolean, boolean, text[]) to authenticated;
grant execute on function public.rpc_actualizar_institucion(
  uuid, text, text, text, text, text, text, boolean, boolean, boolean, text[]) to authenticated;
grant execute on function public.rpc_obtener_configuracion_institucion(uuid) to service_role;
grant execute on function public.rpc_crear_institucion(
  text, text, text, text, text, text, boolean, boolean, boolean, text[]) to service_role;
grant execute on function public.rpc_actualizar_institucion(
  uuid, text, text, text, text, text, text, boolean, boolean, boolean, text[]) to service_role;

insert into public.schema_migrations (version, nombre, checksum)
values ('013', 'configuracion_centro_educativo', null)
on conflict (version) do nothing;

commit;

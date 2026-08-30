-- Fase 1C - Bloque 3. Seguridad Supabase, RLS, privilegios y RPC.
-- Dependencias: 007 RBAC, 008 modelo academico y auth.uid() de Supabase.

begin;

do $$
begin
  if to_regprocedure('auth.uid()') is null then
    raise exception 'Migracion 009 requiere auth.uid() del entorno Supabase.';
  end if;
  if not exists (select 1 from pg_roles where rolname = 'authenticated')
     or not exists (select 1 from pg_roles where rolname = 'anon')
     or not exists (select 1 from pg_roles where rolname = 'service_role') then
    raise exception 'Migracion 009 requiere roles anon, authenticated y service_role.';
  end if;
end;
$$;

insert into public.permisos (codigo, modulo, nombre)
values
  ('academico.secciones.ver', 'academico', 'Ver secciones'),
  ('academico.secciones.crear', 'academico', 'Crear secciones'),
  ('academico.secciones.editar', 'academico', 'Editar secciones'),
  ('academico.secciones.desactivar', 'academico', 'Desactivar secciones'),
  ('academico.matriculas.cambiar_estado', 'academico', 'Cambiar estado de matriculas'),
  ('academico.responsables.ver', 'academico', 'Ver responsables'),
  ('academico.responsables.crear', 'academico', 'Crear responsables'),
  ('academico.responsables.editar', 'academico', 'Editar responsables')
on conflict (codigo) do nothing;

insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r cross join public.permisos p
where r.codigo = 'admin'
on conflict do nothing;

insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
join public.permisos p on p.codigo in (
  'academico.secciones.ver',
  'academico.secciones.crear',
  'academico.secciones.editar',
  'academico.matriculas.cambiar_estado',
  'academico.responsables.ver',
  'academico.responsables.crear',
  'academico.responsables.editar'
)
where r.codigo = 'operador'
on conflict do nothing;

alter function public.usuario_tiene_permiso(uuid, text, uuid)
  security definer;
alter function public.usuario_tiene_permiso(uuid, text, uuid)
  set search_path = pg_catalog, public, pg_temp;

create or replace function public.usuario_actual_id()
returns uuid
language sql
stable
security definer
set search_path = pg_catalog, public, pg_temp
as $$
  select u.id
  from public.usuarios u
  where u.auth_user_id = auth.uid() and u.activo = true;
$$;

create or replace function public.usuario_tiene_permiso_actual(
  p_permiso_codigo text,
  p_institucion_id uuid default null
)
returns boolean
language sql
stable
security definer
set search_path = pg_catalog, public, pg_temp
as $$
  select coalesce(public.usuario_tiene_permiso(
    auth.uid(), p_permiso_codigo, p_institucion_id
  ), false);
$$;

create or replace function public.usuario_tiene_permiso_en_algun_ambito(p_permiso_codigo text)
returns boolean
language sql
stable
security definer
set search_path = pg_catalog, public, pg_temp
as $$
  select exists (
    select 1
    from public.usuarios u
    join public.usuarios_roles ur on ur.usuario_id = u.id and ur.activo
    join public.roles r on r.id = ur.rol_id and r.activo
    join public.roles_permisos rp on rp.rol_id = r.id
    join public.permisos p on p.id = rp.permiso_id
    where u.auth_user_id = auth.uid() and u.activo and p.codigo = p_permiso_codigo
  );
$$;

create or replace function public.usuario_puede_ver_institucion(p_institucion_id uuid)
returns boolean
language sql
stable
security definer
set search_path = pg_catalog, public, pg_temp
as $$
  select
    public.usuario_tiene_permiso_actual('academico.alumnos.ver', p_institucion_id)
    or public.usuario_tiene_permiso_actual('academico.secciones.ver', p_institucion_id)
    or public.usuario_tiene_permiso_actual('academico.matriculas.ver', p_institucion_id)
    or public.usuario_tiene_permiso_actual('academico.responsables.ver', p_institucion_id);
$$;

create or replace function public.usuario_puede_ver_ciclo(p_ciclo_id uuid)
returns boolean
language sql
stable
security definer
set search_path = pg_catalog, public, pg_temp
as $$
  select exists (
    select 1 from public.ciclos_escolares c
    where c.id = p_ciclo_id and public.usuario_puede_ver_institucion(c.institucion_id)
  );
$$;

create or replace function public.usuario_puede_ver_alumno(p_alumno_id uuid)
returns boolean
language sql
stable
security definer
set search_path = pg_catalog, public, pg_temp
as $$
  select exists (
    select 1
    from public.alumnos a
    join public.usuarios u on u.auth_user_id = auth.uid() and u.activo
    where a.id = p_alumno_id
      and (
        public.usuario_tiene_permiso(auth.uid(), 'academico.alumnos.ver', a.institucion_id)
        or a.persona_id = u.persona_id
        or exists (
          select 1
          from public.responsables r
          join public.alumno_responsable ar on ar.responsable_id = r.id
          where r.persona_id = u.persona_id
            and r.institucion_id = a.institucion_id
            and r.estado = 'activo'
            and ar.alumno_id = a.id
            and ar.estado = 'activo'
        )
      )
  );
$$;

create or replace function public.usuario_puede_ver_responsable(p_responsable_id uuid)
returns boolean
language sql
stable
security definer
set search_path = pg_catalog, public, pg_temp
as $$
  select exists (
    select 1
    from public.responsables r
    join public.usuarios u on u.auth_user_id = auth.uid() and u.activo
    where r.id = p_responsable_id
      and (
        r.persona_id = u.persona_id
        or public.usuario_tiene_permiso(auth.uid(), 'academico.responsables.ver', r.institucion_id)
      )
  );
$$;

create or replace function public.usuario_puede_ver_matricula(p_matricula_id uuid)
returns boolean
language sql
stable
security definer
set search_path = pg_catalog, public, pg_temp
as $$
  select exists (
    select 1 from public.matriculas m
    where m.id = p_matricula_id
      and (
        public.usuario_tiene_permiso(auth.uid(), 'academico.matriculas.ver', m.institucion_id)
        or public.usuario_puede_ver_alumno(m.alumno_id)
      )
  );
$$;

create or replace function public.rpc_crear_alumno_nueva_persona(
  p_institucion_id uuid, p_nombres text, p_apellidos text,
  p_fecha_nacimiento date default null, p_rne text default null,
  p_codigo_interno text default null
)
returns uuid language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
begin
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.alumnos.crear', p_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  return public.crear_alumno_nueva_persona(
    p_institucion_id, p_nombres, p_apellidos, p_fecha_nacimiento, p_rne, p_codigo_interno);
end;
$$;

create or replace function public.rpc_crear_alumno_para_persona(
  p_persona_id uuid, p_institucion_id uuid, p_fecha_nacimiento date default null,
  p_rne text default null, p_codigo_interno text default null
)
returns uuid language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
begin
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.alumnos.crear', p_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  return public.crear_alumno_para_persona(
    p_persona_id, p_institucion_id, p_fecha_nacimiento, p_rne, p_codigo_interno);
end;
$$;

create or replace function public.rpc_crear_seccion(
  p_institucion_id uuid, p_ciclo_id uuid, p_grado_id uuid,
  p_jornada_id uuid, p_nombre text, p_cupo integer default null
)
returns uuid language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
begin
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'academico.secciones.crear', p_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  return public.crear_seccion(
    p_institucion_id, p_ciclo_id, p_grado_id, p_jornada_id, p_nombre, p_cupo);
end;
$$;

create or replace function public.rpc_matricular_alumno(
  p_alumno_id uuid, p_seccion_id uuid, p_periodo_matricula_id uuid
)
returns uuid language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid; v_usuario_id uuid;
begin
  select institucion_id into v_institucion_id from public.secciones where id = p_seccion_id;
  v_usuario_id := public.usuario_actual_id();
  if v_usuario_id is null or v_institucion_id is null or not public.usuario_tiene_permiso_actual(
    'academico.matriculas.crear', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  return public.matricular_alumno(
    p_alumno_id, p_seccion_id, p_periodo_matricula_id, v_usuario_id);
end;
$$;

create or replace function public.rpc_cambiar_estado_matricula(
  p_matricula_id uuid, p_estado_nuevo text, p_motivo text default null
)
returns void language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid; v_usuario_id uuid;
begin
  select institucion_id into v_institucion_id from public.matriculas where id = p_matricula_id;
  v_usuario_id := public.usuario_actual_id();
  if v_usuario_id is null or v_institucion_id is null or not public.usuario_tiene_permiso_actual(
    'academico.matriculas.cambiar_estado', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  perform public.cambiar_estado_matricula(
    p_matricula_id, p_estado_nuevo, v_usuario_id, p_motivo);
end;
$$;

create or replace function public.rpc_desactivar_alumno(p_alumno_id uuid, p_motivo text)
returns void language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid; v_usuario_id uuid;
begin
  select institucion_id into v_institucion_id from public.alumnos where id = p_alumno_id;
  v_usuario_id := public.usuario_actual_id();
  if v_usuario_id is null or v_institucion_id is null or not public.usuario_tiene_permiso_actual(
    'academico.alumnos.desactivar', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  perform public.desactivar_alumno(p_alumno_id, v_usuario_id, p_motivo);
end;
$$;

create or replace function public.rpc_reactivar_alumno(p_alumno_id uuid)
returns void language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid; v_usuario_id uuid;
begin
  select institucion_id into v_institucion_id from public.alumnos where id = p_alumno_id;
  v_usuario_id := public.usuario_actual_id();
  if v_usuario_id is null or v_institucion_id is null or not public.usuario_tiene_permiso_actual(
    'academico.alumnos.editar', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  perform public.reactivar_alumno(p_alumno_id, v_usuario_id);
end;
$$;

create or replace function public.rpc_desactivar_seccion(p_seccion_id uuid, p_motivo text)
returns void language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid;
begin
  select institucion_id into v_institucion_id from public.secciones where id = p_seccion_id;
  if public.usuario_actual_id() is null or v_institucion_id is null
     or not public.usuario_tiene_permiso_actual(
       'academico.secciones.desactivar', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  perform public.desactivar_seccion(p_seccion_id, p_motivo);
end;
$$;

create or replace function public.rpc_asignar_rol_usuario(
  p_usuario_id uuid, p_rol_codigo text, p_institucion_id uuid default null
)
returns uuid language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_rol_id uuid; v_id uuid;
begin
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'identidad.usuarios.asignar_roles', p_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if not exists (select 1 from public.usuarios where id = p_usuario_id and activo) then
    raise exception 'El usuario destino no existe o esta inactivo.' using errcode = '23503';
  end if;
  select id into v_rol_id from public.roles where codigo = p_rol_codigo and activo;
  if v_rol_id is null then
    raise exception 'El rol no existe o esta inactivo.' using errcode = '23503';
  end if;
  if p_institucion_id is not null and not exists (
    select 1 from public.instituciones where id = p_institucion_id and activo
  ) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;
  insert into public.usuarios_roles (usuario_id, rol_id, institucion_id)
  values (p_usuario_id, v_rol_id, p_institucion_id) returning id into v_id;
  return v_id;
end;
$$;

create or replace function public.rpc_desactivar_rol_usuario(
  p_usuario_rol_id uuid, p_motivo text
)
returns void language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_institucion_id uuid;
begin
  if p_motivo is null or btrim(p_motivo) = '' then
    raise exception 'El motivo es obligatorio.' using errcode = '22023';
  end if;
  select institucion_id into v_institucion_id
  from public.usuarios_roles where id = p_usuario_rol_id and activo for update;
  if not found then raise exception 'La asignacion activa no existe.' using errcode = 'P0002'; end if;
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'identidad.usuarios.asignar_roles', v_institucion_id) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  update public.usuarios_roles
  set activo = false, fecha_desactivacion = now(),
      motivo_desactivacion = btrim(p_motivo), updated_at = now()
  where id = p_usuario_rol_id;
end;
$$;

alter table public.personas enable row level security;
alter table public.usuarios enable row level security;
alter table public.roles enable row level security;
alter table public.permisos enable row level security;
alter table public.usuarios_roles enable row level security;
alter table public.roles_permisos enable row level security;
alter table public.alumnos enable row level security;
alter table public.responsables enable row level security;
alter table public.alumno_responsable enable row level security;
alter table public.ciclos_escolares enable row level security;
alter table public.grados enable row level security;
alter table public.jornadas enable row level security;
alter table public.secciones enable row level security;
alter table public.periodos_matricula enable row level security;
alter table public.matriculas enable row level security;
alter table public.matricula_estado_historial enable row level security;

drop policy if exists personas_select on public.personas;
create policy personas_select on public.personas for select to authenticated using (
  id = (select persona_id from public.usuarios where id = public.usuario_actual_id())
  or public.usuario_tiene_permiso_en_algun_ambito('identidad.usuarios.ver')
);
drop policy if exists usuarios_select on public.usuarios;
create policy usuarios_select on public.usuarios for select to authenticated using (
  id = public.usuario_actual_id()
  or public.usuario_tiene_permiso_en_algun_ambito('identidad.usuarios.ver')
);
drop policy if exists roles_select on public.roles;
create policy roles_select on public.roles for select to authenticated using (
  public.usuario_tiene_permiso_en_algun_ambito('identidad.roles.ver')
);
drop policy if exists permisos_select on public.permisos;
create policy permisos_select on public.permisos for select to authenticated using (
  public.usuario_tiene_permiso_en_algun_ambito('identidad.roles.ver')
);
drop policy if exists roles_permisos_select on public.roles_permisos;
create policy roles_permisos_select on public.roles_permisos for select to authenticated using (
  public.usuario_tiene_permiso_en_algun_ambito('identidad.roles.ver')
);
drop policy if exists usuarios_roles_select on public.usuarios_roles;
create policy usuarios_roles_select on public.usuarios_roles for select to authenticated using (
  usuario_id = public.usuario_actual_id()
  or public.usuario_tiene_permiso_actual('identidad.usuarios.ver', institucion_id)
);
drop policy if exists alumnos_select on public.alumnos;
create policy alumnos_select on public.alumnos for select to authenticated using (
  public.usuario_puede_ver_alumno(id)
);
drop policy if exists responsables_select on public.responsables;
create policy responsables_select on public.responsables for select to authenticated using (
  public.usuario_puede_ver_responsable(id)
);
drop policy if exists alumno_responsable_select on public.alumno_responsable;
create policy alumno_responsable_select on public.alumno_responsable for select to authenticated using (
  public.usuario_puede_ver_alumno(alumno_id)
);
drop policy if exists ciclos_select on public.ciclos_escolares;
create policy ciclos_select on public.ciclos_escolares for select to authenticated using (
  public.usuario_puede_ver_institucion(institucion_id)
);
drop policy if exists grados_select on public.grados;
create policy grados_select on public.grados for select to authenticated using (
  public.usuario_tiene_permiso_en_algun_ambito('academico.secciones.ver')
  or public.usuario_tiene_permiso_en_algun_ambito('academico.matriculas.ver')
);
drop policy if exists jornadas_select on public.jornadas;
create policy jornadas_select on public.jornadas for select to authenticated using (
  public.usuario_tiene_permiso_en_algun_ambito('academico.secciones.ver')
  or public.usuario_tiene_permiso_en_algun_ambito('academico.matriculas.ver')
);
drop policy if exists secciones_select on public.secciones;
create policy secciones_select on public.secciones for select to authenticated using (
  public.usuario_tiene_permiso_actual('academico.secciones.ver', institucion_id)
  or public.usuario_tiene_permiso_actual('academico.matriculas.ver', institucion_id)
);
drop policy if exists periodos_matricula_select on public.periodos_matricula;
create policy periodos_matricula_select on public.periodos_matricula for select to authenticated using (
  public.usuario_puede_ver_ciclo(ciclo_id)
);
drop policy if exists matriculas_select on public.matriculas;
create policy matriculas_select on public.matriculas for select to authenticated using (
  public.usuario_puede_ver_matricula(id)
);
drop policy if exists matricula_historial_select on public.matricula_estado_historial;
create policy matricula_historial_select on public.matricula_estado_historial for select to authenticated using (
  public.usuario_puede_ver_matricula(matricula_id)
);

revoke all privileges on all tables in schema public from anon, authenticated;
grant select on public.personas, public.usuarios, public.roles, public.permisos,
  public.usuarios_roles, public.roles_permisos, public.alumnos,
  public.responsables, public.alumno_responsable, public.ciclos_escolares,
  public.grados, public.jornadas, public.secciones, public.periodos_matricula,
  public.matriculas, public.matricula_estado_historial to authenticated;
grant all privileges on all tables in schema public to service_role;

revoke execute on all functions in schema public from public, anon, authenticated;
grant execute on function public.usuario_actual_id() to authenticated;
grant execute on function public.usuario_tiene_permiso_actual(text, uuid) to authenticated;
grant execute on function public.usuario_tiene_permiso_en_algun_ambito(text) to authenticated;
grant execute on function public.usuario_puede_ver_institucion(uuid) to authenticated;
grant execute on function public.usuario_puede_ver_ciclo(uuid) to authenticated;
grant execute on function public.usuario_puede_ver_alumno(uuid) to authenticated;
grant execute on function public.usuario_puede_ver_responsable(uuid) to authenticated;
grant execute on function public.usuario_puede_ver_matricula(uuid) to authenticated;
grant execute on function public.rpc_crear_alumno_nueva_persona(uuid, text, text, date, text, text) to authenticated;
grant execute on function public.rpc_crear_alumno_para_persona(uuid, uuid, date, text, text) to authenticated;
grant execute on function public.rpc_crear_seccion(uuid, uuid, uuid, uuid, text, integer) to authenticated;
grant execute on function public.rpc_matricular_alumno(uuid, uuid, uuid) to authenticated;
grant execute on function public.rpc_cambiar_estado_matricula(uuid, text, text) to authenticated;
grant execute on function public.rpc_desactivar_alumno(uuid, text) to authenticated;
grant execute on function public.rpc_reactivar_alumno(uuid) to authenticated;
grant execute on function public.rpc_desactivar_seccion(uuid, text) to authenticated;
grant execute on function public.rpc_asignar_rol_usuario(uuid, text, uuid) to authenticated;
grant execute on function public.rpc_desactivar_rol_usuario(uuid, text) to authenticated;
grant execute on all functions in schema public to service_role;

insert into public.schema_migrations (version, nombre, checksum)
values ('009', 'seguridad_rls_rpc', null)
on conflict (version) do nothing;

commit;

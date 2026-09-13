-- Rollback 028 - retira la estructura de RBAC dinamico introducida por 028.
--
-- Seguridad: aborta si ya existen asignaciones a roles nuevos, roles
-- institucionales creados despues de la migracion o permisos platform.*
-- concedidos fuera del platform_admin sembrado. No destruye configuracion
-- RBAC que pudiera haberse creado posteriormente.

begin;

do $$
begin
  if exists (
    select 1
    from public.usuarios_roles ur
    join public.roles r on r.id = ur.rol_id
    where r.tipo <> 'legacy'
  ) then
    raise exception 'Rollback 028 bloqueado: existen asignaciones a roles no legacy.';
  end if;

  if exists (
    select 1 from public.roles
    where tipo = 'institucional'
  ) then
    raise exception 'Rollback 028 bloqueado: existen roles institucionales creados.';
  end if;

  if exists (
    select 1
    from public.roles r
    where r.tipo <> 'legacy'
      and r.codigo not in (
        'platform_admin',
        'school_admin',
        'school_staff',
        'academic_coordinator',
        'finance_operator',
        'teacher',
        'parent',
        'student',
        'demo_viewer',
        'support_agent'
      )
  ) then
    raise exception 'Rollback 028 bloqueado: existen definiciones de rol posteriores a 028.';
  end if;

  if exists (
    select 1
    from public.roles_permisos rp
    join public.permisos p on p.id = rp.permiso_id
    join public.roles r on r.id = rp.rol_id
    where p.codigo like 'platform.%'
      and r.codigo <> 'platform_admin'
  ) then
    raise exception 'Rollback 028 bloqueado: permisos platform.* fueron concedidos a otros roles.';
  end if;
end
$$;

-- Restaurar las RPC previas de gestion de asignaciones.
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

revoke execute on function public.rpc_asignar_rol_usuario(uuid, text, uuid)
  from public, anon;
revoke execute on function public.rpc_desactivar_rol_usuario(uuid, text)
  from public, anon;
grant execute on function public.rpc_asignar_rol_usuario(uuid, text, uuid)
  to authenticated, service_role;
grant execute on function public.rpc_desactivar_rol_usuario(uuid, text)
  to authenticated, service_role;

-- Restaurar las policies de lectura de 009.
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

-- Retirar el invariante de ambito agregado por 028.
drop trigger if exists trg_usuarios_roles_validar_ambito_rol_before
  on public.usuarios_roles;
drop function if exists public.trg_usuarios_roles_validar_ambito_rol();

-- Las relaciones de roles_permisos se borran por ON DELETE CASCADE.
delete from public.roles
where tipo in ('plataforma', 'plantilla')
  and codigo in (
    'platform_admin',
    'school_admin',
    'school_staff',
    'academic_coordinator',
    'finance_operator',
    'teacher',
    'parent',
    'student',
    'demo_viewer',
    'support_agent'
  );

delete from public.permisos
where codigo in (
  'platform.superadmins.gestionar',
  'platform.roles.ver',
  'platform.roles.editar',
  'platform.permisos.ver',
  'platform.permisos.editar',
  'platform.auditoria.ver'
);

-- Restaurar la unicidad global original de roles.
drop index if exists public.ix_roles_rol_base_id;
drop index if exists public.ix_roles_institucion_tipo_activo;
drop index if exists public.ux_roles_institucion_codigo;
drop index if exists public.ux_roles_codigo_global;

alter table public.roles drop constraint if exists fk_roles_rol_base;
alter table public.roles drop constraint if exists fk_roles_institucion;
alter table public.roles drop constraint if exists ck_roles_plantilla_version;
alter table public.roles drop constraint if exists ck_roles_rol_base_distinto;
alter table public.roles drop constraint if exists ck_roles_tipo_ambito;
alter table public.roles drop constraint if exists ck_roles_tipo;

alter table public.roles
  drop column if exists plantilla_version,
  drop column if exists protegido,
  drop column if exists rol_base_id,
  drop column if exists tipo,
  drop column if exists institucion_id;

alter table public.roles add constraint uq_roles_codigo unique (codigo);

alter table public.permisos drop constraint if exists ck_permisos_plataforma_no_delegable;
alter table public.permisos drop constraint if exists ck_permisos_estado;
alter table public.permisos drop constraint if exists ck_permisos_riesgo;
alter table public.permisos drop constraint if exists ck_permisos_ambito;

alter table public.permisos
  drop column if exists visible_en_roles,
  drop column if exists estado,
  drop column if exists riesgo,
  drop column if exists delegable,
  drop column if exists ambito;

delete from public.schema_migrations where version = '028';

commit;

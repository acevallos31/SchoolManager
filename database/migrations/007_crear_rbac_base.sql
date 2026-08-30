-- Fase 1C - Bloque 1. RBAC aditivo y compatible con usuarios.rol.
-- Dependencias: 002_instituciones y 003_usuarios con auth_user_id.
-- La columna usuarios.rol se conserva temporalmente para no romper el backend.

begin;

create extension if not exists pgcrypto;

create table if not exists public.roles (
  id uuid primary key default gen_random_uuid(),
  codigo text not null,
  nombre text not null,
  descripcion text null,
  es_sistema boolean not null default false,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint uq_roles_codigo unique (codigo),
  constraint ck_roles_codigo_formato check (
    codigo = lower(btrim(codigo)) and codigo ~ '^[a-z][a-z0-9_]*$'
  ),
  constraint ck_roles_nombre_no_vacio check (btrim(nombre) <> '')
);

create table if not exists public.permisos (
  id uuid primary key default gen_random_uuid(),
  codigo text not null,
  modulo text not null,
  nombre text not null,
  descripcion text null,
  created_at timestamptz not null default now(),
  constraint uq_permisos_codigo unique (codigo),
  constraint ck_permisos_codigo_formato check (
    codigo = lower(btrim(codigo))
    and codigo ~ '^[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*$'
  ),
  constraint ck_permisos_modulo_formato check (
    modulo = lower(btrim(modulo))
    and modulo ~ '^[a-z][a-z0-9_]*$'
    and split_part(codigo, '.', 1) = modulo
  ),
  constraint ck_permisos_nombre_no_vacio check (btrim(nombre) <> '')
);

create table if not exists public.usuarios_roles (
  id uuid primary key default gen_random_uuid(),
  usuario_id uuid not null,
  rol_id uuid not null,
  institucion_id uuid null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
  constraint fk_usuarios_roles_usuario foreign key (usuario_id)
    references public.usuarios(id) on delete restrict,
  constraint fk_usuarios_roles_rol foreign key (rol_id)
    references public.roles(id) on delete restrict,
  constraint fk_usuarios_roles_institucion foreign key (institucion_id)
    references public.instituciones(id) on delete restrict,
  constraint ck_usuarios_roles_desactivacion check (
    (activo and fecha_desactivacion is null and motivo_desactivacion is null)
    or (
      not activo
      and fecha_desactivacion is not null
      and motivo_desactivacion is not null
      and btrim(motivo_desactivacion) <> ''
    )
  )
);

create table if not exists public.roles_permisos (
  rol_id uuid not null,
  permiso_id uuid not null,
  created_at timestamptz not null default now(),
  constraint pk_roles_permisos primary key (rol_id, permiso_id),
  constraint fk_roles_permisos_rol foreign key (rol_id)
    references public.roles(id) on delete cascade,
  constraint fk_roles_permisos_permiso foreign key (permiso_id)
    references public.permisos(id) on delete cascade
);

create unique index if not exists ux_usuarios_roles_global_activo
  on public.usuarios_roles (usuario_id, rol_id)
  where institucion_id is null and activo = true;

create unique index if not exists ux_usuarios_roles_institucion_activo
  on public.usuarios_roles (usuario_id, rol_id, institucion_id)
  where institucion_id is not null and activo = true;

create index if not exists ix_usuarios_roles_usuario_ambito_activo
  on public.usuarios_roles (usuario_id, institucion_id, activo);
create index if not exists ix_usuarios_roles_rol_id
  on public.usuarios_roles (rol_id);
create index if not exists ix_roles_permisos_permiso_id
  on public.roles_permisos (permiso_id);

insert into public.roles (codigo, nombre, descripcion, es_sistema)
values
  ('admin', 'Administrador', 'Administracion integral del sistema.', true),
  ('operador', 'Operador', 'Operacion academica cotidiana.', true),
  ('usuario', 'Usuario', 'Compatibilidad temporal con el rol legacy usuario.', true),
  ('padre', 'Padre o responsable', 'Consulta vinculada a sus representados.', true),
  ('docente', 'Docente', 'Reservado para el futuro modulo de docencia.', true),
  ('cajero', 'Cajero', 'Reservado para el futuro modulo de finanzas.', true),
  ('consulta', 'Consulta', 'Acceso de solo lectura segun permisos asignados.', true)
on conflict (codigo) do nothing;

insert into public.permisos (codigo, modulo, nombre)
values
  ('academico.alumnos.ver', 'academico', 'Ver alumnos'),
  ('academico.alumnos.crear', 'academico', 'Crear alumnos'),
  ('academico.alumnos.editar', 'academico', 'Editar alumnos'),
  ('academico.alumnos.desactivar', 'academico', 'Desactivar alumnos'),
  ('academico.matriculas.ver', 'academico', 'Ver matriculas'),
  ('academico.matriculas.crear', 'academico', 'Crear matriculas'),
  ('academico.matriculas.editar', 'academico', 'Editar matriculas'),
  ('academico.matriculas.anular', 'academico', 'Anular matriculas'),
  ('responsables.responsables.ver', 'responsables', 'Ver responsables'),
  ('responsables.responsables.crear', 'responsables', 'Crear responsables'),
  ('responsables.responsables.editar', 'responsables', 'Editar responsables'),
  ('identidad.usuarios.ver', 'identidad', 'Ver usuarios'),
  ('identidad.usuarios.crear', 'identidad', 'Crear usuarios'),
  ('identidad.usuarios.editar', 'identidad', 'Editar usuarios'),
  ('identidad.usuarios.asignar_roles', 'identidad', 'Asignar roles a usuarios'),
  ('identidad.roles.ver', 'identidad', 'Ver roles'),
  ('identidad.roles.crear', 'identidad', 'Crear roles'),
  ('identidad.roles.editar', 'identidad', 'Editar roles'),
  ('identidad.roles.asignar_permisos', 'identidad', 'Asignar permisos a roles')
on conflict (codigo) do nothing;

-- Admin recibe el conjunto base completo. Operador solo opera el nucleo academico.
insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'admin'
on conflict do nothing;

insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
join public.permisos p on p.codigo in (
  'academico.alumnos.ver',
  'academico.alumnos.crear',
  'academico.alumnos.editar',
  'academico.matriculas.ver',
  'academico.matriculas.crear',
  'responsables.responsables.ver',
  'responsables.responsables.crear',
  'responsables.responsables.editar'
)
where r.codigo = 'operador'
on conflict do nothing;

do $$
begin
  if exists (
    select 1
    from public.usuarios u
    left join public.roles r on r.codigo = u.rol
    where r.id is null
  ) then
    raise exception 'RBAC no puede migrar usuarios con roles legacy desconocidos.';
  end if;
end;
$$;

-- El esquema legacy no expresa institucion del usuario: la migracion inicial es global.
insert into public.usuarios_roles (usuario_id, rol_id, institucion_id)
select u.id, r.id, null
from public.usuarios u
join public.roles r on r.codigo = u.rol
where not exists (
  select 1
  from public.usuarios_roles ur
  where ur.usuario_id = u.id
    and ur.rol_id = r.id
    and ur.institucion_id is null
    and ur.activo = true
);

create or replace function public.usuario_tiene_permiso(
  p_auth_user_id uuid,
  p_permiso_codigo text,
  p_institucion_id uuid default null
)
returns boolean
language sql
stable
security invoker
set search_path = pg_catalog, public
as $$
  select exists (
    select 1
    from public.usuarios u
    join public.usuarios_roles ur on ur.usuario_id = u.id
    join public.roles r on r.id = ur.rol_id
    join public.roles_permisos rp on rp.rol_id = r.id
    join public.permisos p on p.id = rp.permiso_id
    where u.auth_user_id = p_auth_user_id
      and u.activo = true
      and ur.activo = true
      and r.activo = true
      and p.codigo = p_permiso_codigo
      and (
        ur.institucion_id is null
        or (p_institucion_id is not null and ur.institucion_id = p_institucion_id)
      )
  );
$$;

comment on table public.usuarios_roles is
  'Asignaciones historizables; institucion_id NULL representa un rol global.';
comment on table public.roles_permisos is
  'Relacion vigente sin historial propio; sus FK usan ON DELETE CASCADE.';
comment on function public.usuario_tiene_permiso(uuid, text, uuid) is
  'Evalua permisos globales y, cuando se indica, del ambito institucional exacto.';

insert into public.schema_migrations (version, nombre, checksum)
values ('007', 'crear_rbac_base', null)
on conflict (version) do nothing;

commit;

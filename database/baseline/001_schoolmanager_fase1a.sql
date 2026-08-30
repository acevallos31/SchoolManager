-- SchoolManager - Baseline consolidado actual (Fase 1C).
-- Solo para instalaciones nuevas. Integra identidad normalizada, RBAC multirol,
-- modelo academico, historico, garantias ACID, RLS y RPC seguras.
-- El nombre fisico se conserva por compatibilidad con automatizacion existente.

begin;

create extension if not exists pgcrypto;

create table public.schema_migrations (
  version text primary key,
  nombre text not null,
  aplicado_en timestamptz not null default now(),
  checksum text null,
  constraint ck_schema_migrations_version_no_vacia check (btrim(version) <> ''),
  constraint ck_schema_migrations_nombre_no_vacio check (btrim(nombre) <> '')
);

create table public.instituciones (
  id uuid primary key default gen_random_uuid(),
  nombre text not null,
  nombre_corto text null,
  direccion text null,
  telefono text null,
  correo text null,
  logo_url text null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
  constraint ck_instituciones_nombre_no_vacio check (btrim(nombre) <> '')
);

create table public.configuracion_identificadores (
  id uuid primary key default gen_random_uuid(),
  institucion_id uuid not null references public.instituciones(id),
  rne_requerido boolean not null default false,
  identificacion_civil_requerida boolean not null default false,
  codigo_interno_requerido boolean not null default false,
  tipos_identificacion_permitidos text[] not null default '{}',
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint uq_configuracion_identificadores_institucion unique (institucion_id)
);

create table public.personas (
  id uuid primary key default gen_random_uuid(),
  nombres text not null,
  apellidos text not null,
  tipo_identificacion text null,
  numero_identificacion text null,
  numero_identificacion_normalizado text null,
  pais_emisor text null,
  telefono text null,
  correo text null,
  direccion text null,
  estado text not null default 'activo',
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
  constraint ck_personas_nombres_no_vacios check (btrim(nombres) <> ''),
  constraint ck_personas_apellidos_no_vacios check (btrim(apellidos) <> ''),
  constraint ck_personas_estado check (estado in ('activo', 'inactivo')),
  constraint ck_personas_identificacion_coherente check (
    (
      numero_identificacion is null
      and tipo_identificacion is null
      and numero_identificacion_normalizado is null
      and pais_emisor is null
    )
    or (
      numero_identificacion is not null
      and btrim(numero_identificacion) <> ''
      and tipo_identificacion is not null
      and btrim(tipo_identificacion) <> ''
      and numero_identificacion_normalizado is not null
      and btrim(numero_identificacion_normalizado) <> ''
    )
  )
);

create table public.usuarios (
  id uuid primary key default gen_random_uuid(),
  persona_id uuid not null references public.personas(id),
  auth_user_id uuid not null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null
);

create table public.roles (
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

create table public.permisos (
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

create table public.usuarios_roles (
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

create table public.roles_permisos (
  rol_id uuid not null,
  permiso_id uuid not null,
  created_at timestamptz not null default now(),
  constraint pk_roles_permisos primary key (rol_id, permiso_id),
  constraint fk_roles_permisos_rol foreign key (rol_id)
    references public.roles(id) on delete cascade,
  constraint fk_roles_permisos_permiso foreign key (permiso_id)
    references public.permisos(id) on delete cascade
);

create table public.alumnos (
  id uuid primary key default gen_random_uuid(),
  persona_id uuid not null references public.personas(id) on delete restrict,
  institucion_id uuid not null references public.instituciones(id) on delete restrict,
  rne text null,
  codigo_interno text null,
  fecha_nacimiento date null,
  estado text not null default 'activo',
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
  constraint uq_alumnos_id_institucion unique (id, institucion_id),
  constraint ck_alumnos_estado check (estado in ('activo', 'inactivo'))
);

create table public.ciclos_escolares (
  id uuid primary key default gen_random_uuid(),
  institucion_id uuid not null references public.instituciones(id) on delete restrict,
  nombre text not null,
  fecha_inicio date null,
  fecha_fin date null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
  constraint uq_ciclos_id_institucion unique (id, institucion_id),
  constraint uq_ciclos_escolares_institucion_nombre unique (institucion_id, nombre),
  constraint ck_ciclos_escolares_nombre_no_vacio check (btrim(nombre) <> ''),
  constraint ck_ciclos_escolares_fechas check (
    fecha_inicio is null or fecha_fin is null or fecha_inicio <= fecha_fin
  )
);

create table public.grados (
  id uuid primary key default gen_random_uuid(),
  nombre text not null,
  orden integer not null default 0,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint uq_grados_nombre unique (nombre),
  constraint ck_grados_nombre_no_vacio check (btrim(nombre) <> '')
);

create table public.jornadas (
  id uuid primary key default gen_random_uuid(),
  nombre text not null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint uq_jornadas_nombre unique (nombre),
  constraint ck_jornadas_nombre_no_vacio check (btrim(nombre) <> '')
);

create table public.secciones (
  id uuid primary key default gen_random_uuid(),
  institucion_id uuid not null,
  ciclo_id uuid not null,
  grado_id uuid not null references public.grados(id) on delete restrict,
  jornada_id uuid null references public.jornadas(id) on delete restrict,
  nombre text not null,
  cupo integer null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
  constraint uq_secciones_id_ciclo_institucion unique (id, ciclo_id, institucion_id),
  constraint fk_secciones_ciclo_institucion foreign key (ciclo_id, institucion_id)
    references public.ciclos_escolares(id, institucion_id) on delete restrict,
  constraint ck_secciones_nombre_no_vacio check (btrim(nombre) <> ''),
  constraint ck_secciones_cupo check (cupo is null or cupo > 0)
);

create table public.responsables (
  id uuid primary key default gen_random_uuid(),
  persona_id uuid not null references public.personas(id),
  institucion_id uuid not null references public.instituciones(id),
  estado text not null default 'activo',
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
  constraint uq_responsables_persona_institucion unique (persona_id, institucion_id),
  constraint ck_responsables_estado check (estado in ('activo', 'inactivo'))
);

create table public.alumno_responsable (
  id uuid primary key default gen_random_uuid(),
  alumno_id uuid not null references public.alumnos(id),
  responsable_id uuid not null references public.responsables(id),
  parentesco text null,
  es_principal boolean not null default false,
  acceso_financiero boolean not null default false,
  estado text not null default 'activo',
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
  constraint uq_alumno_responsable unique (alumno_id, responsable_id),
  constraint ck_alumno_responsable_estado check (estado in ('activo', 'inactivo'))
);

create table public.periodos_matricula (
  id uuid primary key default gen_random_uuid(),
  ciclo_id uuid not null references public.ciclos_escolares(id) on delete restrict,
  nombre text not null,
  tipo text null,
  fecha_inicio date not null,
  fecha_fin date not null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint uq_periodos_id_ciclo unique (id, ciclo_id),
  constraint uq_periodos_matricula_ciclo_nombre unique (ciclo_id, nombre),
  constraint ck_periodos_matricula_nombre_no_vacio check (btrim(nombre) <> ''),
  constraint ck_periodos_matricula_fechas check (fecha_inicio <= fecha_fin)
);

create table public.matriculas (
  id uuid primary key default gen_random_uuid(),
  alumno_id uuid not null,
  institucion_id uuid not null,
  ciclo_id uuid not null,
  seccion_id uuid not null,
  periodo_matricula_id uuid not null,
  registrado_por uuid null references public.usuarios(id) on delete restrict,
  fecha_matricula date not null default current_date,
  estado text not null default 'pendiente',
  fecha_anulacion timestamptz null,
  motivo_anulacion text null,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint fk_matriculas_alumno_institucion foreign key (alumno_id, institucion_id)
    references public.alumnos(id, institucion_id) on delete restrict,
  constraint fk_matriculas_seccion_contexto foreign key (seccion_id, ciclo_id, institucion_id)
    references public.secciones(id, ciclo_id, institucion_id) on delete restrict,
  constraint fk_matriculas_periodo_ciclo foreign key (periodo_matricula_id, ciclo_id)
    references public.periodos_matricula(id, ciclo_id) on delete restrict,
  constraint uq_matriculas_alumno_ciclo unique (alumno_id, ciclo_id),
  constraint ck_matriculas_estado check (
    estado in ('pendiente', 'activa', 'finalizada', 'retirada', 'anulada', 'trasladada')
  ),
  constraint ck_matriculas_anulacion_coherente check (
    (estado = 'anulada' and fecha_anulacion is not null
      and motivo_anulacion is not null and btrim(motivo_anulacion) <> '')
    or (estado <> 'anulada' and fecha_anulacion is null and motivo_anulacion is null)
  )
);

create table public.matricula_estado_historial (
  id uuid primary key default gen_random_uuid(),
  matricula_id uuid not null references public.matriculas(id) on delete restrict,
  estado_anterior text null,
  estado_nuevo text not null,
  fecha timestamptz not null default now(),
  usuario_id uuid null references public.usuarios(id) on delete restrict,
  motivo text null,
  constraint ck_matricula_historial_estado_anterior check (
    estado_anterior is null or estado_anterior in
      ('pendiente', 'activa', 'finalizada', 'retirada', 'anulada', 'trasladada')
  ),
  constraint ck_matricula_historial_estado_nuevo check (
    estado_nuevo in ('pendiente', 'activa', 'finalizada', 'retirada', 'anulada', 'trasladada')
  ),
  constraint ck_matricula_historial_motivo_no_vacio check (
    motivo is null or btrim(motivo) <> ''
  )
);

create unique index ux_personas_documento_normalizado
  on public.personas (
    lower(tipo_identificacion),
    lower(coalesce(pais_emisor, '')),
    numero_identificacion_normalizado
  ) where numero_identificacion_normalizado is not null;

create unique index ux_usuarios_auth_user_id on public.usuarios (auth_user_id);
create unique index ux_usuarios_persona_id on public.usuarios (persona_id);

create unique index ux_usuarios_roles_global_activo
  on public.usuarios_roles (usuario_id, rol_id)
  where institucion_id is null and activo = true;
create unique index ux_usuarios_roles_institucion_activo
  on public.usuarios_roles (usuario_id, rol_id, institucion_id)
  where institucion_id is not null and activo = true;
create index ix_usuarios_roles_usuario_ambito_activo
  on public.usuarios_roles (usuario_id, institucion_id, activo);
create index ix_usuarios_roles_rol_id on public.usuarios_roles (rol_id);
create index ix_roles_permisos_permiso_id on public.roles_permisos (permiso_id);

create unique index ux_alumnos_rne_global on public.alumnos (rne)
  where rne is not null;
create unique index ux_alumnos_codigo_interno_por_institucion
  on public.alumnos (institucion_id, lower(codigo_interno))
  where codigo_interno is not null;
create unique index ux_alumnos_persona_institucion
  on public.alumnos (persona_id, institucion_id);

create unique index ux_secciones_contexto_jornada_nombre
  on public.secciones (institucion_id, ciclo_id, grado_id, jornada_id, lower(nombre))
  where jornada_id is not null;
create unique index ux_secciones_contexto_sin_jornada_nombre
  on public.secciones (institucion_id, ciclo_id, grado_id, lower(nombre))
  where jornada_id is null;

create unique index ux_alumno_responsable_principal_activo
  on public.alumno_responsable (alumno_id)
  where es_principal = true and estado = 'activo';

create index ix_alumnos_institucion_id on public.alumnos (institucion_id);
create index ix_ciclos_escolares_institucion_id on public.ciclos_escolares (institucion_id);
create index ix_secciones_grado_id on public.secciones (grado_id);
create index ix_secciones_jornada_id on public.secciones (jornada_id);
create index ix_secciones_ciclo_grado on public.secciones (ciclo_id, grado_id);
create index ix_secciones_institucion_activo on public.secciones (institucion_id, activo);
create index ix_alumno_responsable_responsable_alumno
  on public.alumno_responsable (responsable_id, alumno_id);
create index ix_alumno_responsable_alumno_estado
  on public.alumno_responsable (alumno_id, estado);
create index ix_periodos_matricula_ciclo_vigencia
  on public.periodos_matricula (ciclo_id, activo, fecha_inicio, fecha_fin);
create index ix_matriculas_ciclo_id on public.matriculas (ciclo_id);
create index ix_matriculas_seccion_id on public.matriculas (seccion_id);
create index ix_matriculas_periodo_matricula_id on public.matriculas (periodo_matricula_id);
create index ix_matriculas_institucion_ciclo_estado
  on public.matriculas (institucion_id, ciclo_id, estado);
create index ix_matricula_historial_matricula_fecha
  on public.matricula_estado_historial (matricula_id, fecha);

comment on table public.configuracion_identificadores is
  'La configuracion decide obligatoriedad de identificadores, nunca PK o FK.';
comment on column public.personas.pais_emisor is
  'Nullable; una regla futura podra requerirlo segun tipo documental.';
comment on column public.alumnos.rne is
  'Identificador educativo nullable y unico global cuando existe; comparacion exacta.';

insert into public.roles (codigo, nombre, descripcion, es_sistema)
values
  ('admin', 'Administrador', 'Administracion integral del sistema.', true),
  ('operador', 'Operador', 'Operacion academica cotidiana.', true),
  ('usuario', 'Usuario', 'Compatibilidad temporal con el rol legacy usuario.', true),
  ('padre', 'Padre o responsable', 'Consulta vinculada a sus representados.', true),
  ('docente', 'Docente', 'Reservado para el futuro modulo de docencia.', true),
  ('cajero', 'Cajero', 'Reservado para el futuro modulo de finanzas.', true),
  ('consulta', 'Consulta', 'Acceso de solo lectura segun permisos asignados.', true);

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
  ('identidad.roles.asignar_permisos', 'identidad', 'Asignar permisos a roles');

insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'admin';

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
where r.codigo = 'operador';

create function public.usuario_tiene_permiso(
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

create or replace function public.registrar_historial_estado_matricula()
returns trigger
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare
  v_usuario_id uuid := nullif(current_setting('schoolmanager.usuario_id', true), '')::uuid;
  v_motivo text := nullif(current_setting('schoolmanager.motivo_estado', true), '');
begin
  if tg_op = 'INSERT' or old.estado is distinct from new.estado then
    insert into public.matricula_estado_historial (
      matricula_id, estado_anterior, estado_nuevo, usuario_id, motivo
    ) values (
      new.id,
      case when tg_op = 'INSERT' then null else old.estado end,
      new.estado,
      coalesce(v_usuario_id, case when tg_op = 'INSERT' then new.registrado_por else null end),
      v_motivo
    );
  end if;

  perform set_config('schoolmanager.usuario_id', '', true);
  perform set_config('schoolmanager.motivo_estado', '', true);
  return new;
end;
$$;

drop trigger if exists trg_matriculas_historial_estado on public.matriculas;
create trigger trg_matriculas_historial_estado
after insert or update of estado on public.matriculas
for each row execute function public.registrar_historial_estado_matricula();

create or replace function public.crear_seccion(
  p_institucion_id uuid,
  p_ciclo_id uuid,
  p_grado_id uuid,
  p_jornada_id uuid,
  p_nombre text,
  p_cupo integer default null
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare v_id uuid;
begin
  if p_nombre is null or btrim(p_nombre) = '' then
    raise exception 'El nombre de la seccion es obligatorio.' using errcode = '22023';
  end if;
  if p_cupo is not null and p_cupo <= 0 then
    raise exception 'El cupo debe ser mayor que cero.' using errcode = '22023';
  end if;
  if not exists (select 1 from public.instituciones where id = p_institucion_id and activo) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;
  if not exists (
    select 1 from public.ciclos_escolares
    where id = p_ciclo_id and institucion_id = p_institucion_id and activo
  ) then
    raise exception 'El ciclo no pertenece a la institucion o esta inactivo.' using errcode = '23503';
  end if;
  if not exists (select 1 from public.grados where id = p_grado_id and activo) then
    raise exception 'El grado no existe o esta inactivo.' using errcode = '23503';
  end if;
  if p_jornada_id is not null and not exists (
    select 1 from public.jornadas where id = p_jornada_id and activo
  ) then
    raise exception 'La jornada no existe o esta inactiva.' using errcode = '23503';
  end if;

  insert into public.secciones (
    institucion_id, ciclo_id, grado_id, jornada_id, nombre, cupo
  ) values (
    p_institucion_id, p_ciclo_id, p_grado_id, p_jornada_id, btrim(p_nombre), p_cupo
  ) returning id into v_id;
  return v_id;
end;
$$;

create or replace function public.matricular_alumno(
  p_alumno_id uuid,
  p_seccion_id uuid,
  p_periodo_matricula_id uuid,
  p_registrado_por uuid default null
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare
  v_seccion public.secciones%rowtype;
  v_id uuid;
  v_ocupados integer;
begin
  select * into v_seccion
  from public.secciones
  where id = p_seccion_id
  for update;

  if not found or not v_seccion.activo then
    raise exception 'La seccion no existe o esta inactiva.' using errcode = '23503';
  end if;
  if not exists (
    select 1 from public.ciclos_escolares
    where id = v_seccion.ciclo_id
      and institucion_id = v_seccion.institucion_id
      and activo
  ) then
    raise exception 'El ciclo de la seccion esta inactivo o es incoherente.' using errcode = '23503';
  end if;
  if not exists (
    select 1 from public.alumnos
    where id = p_alumno_id
      and institucion_id = v_seccion.institucion_id
      and estado = 'activo'
  ) then
    raise exception 'El alumno no existe, esta inactivo o pertenece a otra institucion.' using errcode = '23503';
  end if;
  if not exists (
    select 1 from public.periodos_matricula
    where id = p_periodo_matricula_id
      and ciclo_id = v_seccion.ciclo_id
      and activo
  ) then
    raise exception 'El periodo no corresponde al ciclo o esta inactivo.' using errcode = '23503';
  end if;
  if p_registrado_por is not null and not exists (
    select 1 from public.usuarios where id = p_registrado_por and activo
  ) then
    raise exception 'El usuario registrador no existe o esta inactivo.' using errcode = '23503';
  end if;

  if v_seccion.cupo is not null then
    select count(*) into v_ocupados
    from public.matriculas
    where seccion_id = p_seccion_id and estado in ('pendiente', 'activa');
    if v_ocupados >= v_seccion.cupo then
      raise exception 'La seccion alcanzo su cupo.' using errcode = '23514';
    end if;
  end if;

  perform set_config('schoolmanager.usuario_id', coalesce(p_registrado_por::text, ''), true);
  perform set_config('schoolmanager.motivo_estado', 'Matricula creada', true);
  insert into public.matriculas (
    alumno_id, institucion_id, ciclo_id, seccion_id,
    periodo_matricula_id, registrado_por, estado
  ) values (
    p_alumno_id, v_seccion.institucion_id, v_seccion.ciclo_id, p_seccion_id,
    p_periodo_matricula_id, p_registrado_por, 'pendiente'
  ) returning id into v_id;
  return v_id;
end;
$$;

create or replace function public.cambiar_estado_matricula(
  p_matricula_id uuid,
  p_estado_nuevo text,
  p_usuario_id uuid,
  p_motivo text default null
)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare v_estado_actual text;
begin
  select estado into v_estado_actual
  from public.matriculas where id = p_matricula_id for update;
  if not found then
    raise exception 'La matricula no existe.' using errcode = 'P0002';
  end if;
  if not exists (select 1 from public.usuarios where id = p_usuario_id and activo) then
    raise exception 'El usuario no existe o esta inactivo.' using errcode = '23503';
  end if;
  if p_estado_nuevo = v_estado_actual then
    return;
  end if;
  if not (
    (v_estado_actual = 'pendiente' and p_estado_nuevo in ('activa', 'anulada'))
    or (v_estado_actual = 'activa' and p_estado_nuevo in
      ('finalizada', 'retirada', 'anulada', 'trasladada'))
  ) then
    raise exception 'Transicion de estado no permitida: % -> %', v_estado_actual, p_estado_nuevo
      using errcode = '22023';
  end if;
  if p_estado_nuevo in ('retirada', 'anulada', 'trasladada')
     and (p_motivo is null or btrim(p_motivo) = '') then
    raise exception 'El motivo es obligatorio para el estado solicitado.' using errcode = '22023';
  end if;

  perform set_config('schoolmanager.usuario_id', p_usuario_id::text, true);
  perform set_config('schoolmanager.motivo_estado', coalesce(btrim(p_motivo), ''), true);
  update public.matriculas
  set estado = p_estado_nuevo,
      fecha_anulacion = case when p_estado_nuevo = 'anulada' then now() else null end,
      motivo_anulacion = case when p_estado_nuevo = 'anulada' then btrim(p_motivo) else null end,
      updated_at = now()
  where id = p_matricula_id;
end;
$$;

create or replace function public.desactivar_alumno(
  p_alumno_id uuid,
  p_usuario_id uuid,
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
  if not exists (select 1 from public.usuarios where id = p_usuario_id and activo) then
    raise exception 'El usuario no existe o esta inactivo.' using errcode = '23503';
  end if;
  perform 1 from public.alumnos where id = p_alumno_id for update;
  if not found then raise exception 'El alumno no existe.' using errcode = 'P0002'; end if;
  if exists (
    select 1 from public.matriculas
    where alumno_id = p_alumno_id and estado in ('pendiente', 'activa')
  ) then
    raise exception 'El alumno tiene una matricula vigente; procesela antes de desactivarlo.'
      using errcode = '23514';
  end if;
  update public.alumnos
  set estado = 'inactivo', fecha_desactivacion = now(),
      motivo_desactivacion = btrim(p_motivo), updated_at = now()
  where id = p_alumno_id;
end;
$$;

create or replace function public.reactivar_alumno(
  p_alumno_id uuid,
  p_usuario_id uuid
)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
begin
  if not exists (select 1 from public.usuarios where id = p_usuario_id and activo) then
    raise exception 'El usuario no existe o esta inactivo.' using errcode = '23503';
  end if;
  update public.alumnos
  set estado = 'activo', fecha_desactivacion = null,
      motivo_desactivacion = null, updated_at = now()
  where id = p_alumno_id;
  if not found then raise exception 'El alumno no existe.' using errcode = 'P0002'; end if;
end;
$$;

create or replace function public.desactivar_seccion(
  p_seccion_id uuid,
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
  perform 1 from public.secciones where id = p_seccion_id for update;
  if not found then raise exception 'La seccion no existe.' using errcode = 'P0002'; end if;
  if exists (
    select 1 from public.matriculas
    where seccion_id = p_seccion_id and estado in ('pendiente', 'activa')
  ) then
    raise exception 'La seccion tiene matriculas vigentes.' using errcode = '23514';
  end if;
  update public.secciones
  set activo = false, fecha_desactivacion = now(),
      motivo_desactivacion = btrim(p_motivo), updated_at = now()
  where id = p_seccion_id;
end;
$$;

create or replace function public.crear_alumno_nueva_persona(
  p_institucion_id uuid,
  p_nombres text,
  p_apellidos text,
  p_fecha_nacimiento date default null,
  p_rne text default null,
  p_codigo_interno text default null
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare v_persona_id uuid; v_alumno_id uuid;
begin
  if not exists (select 1 from public.instituciones where id = p_institucion_id and activo) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;
  insert into public.personas (nombres, apellidos)
  values (p_nombres, p_apellidos) returning id into v_persona_id;
  insert into public.alumnos (
    persona_id, institucion_id, fecha_nacimiento, rne, codigo_interno
  ) values (
    v_persona_id, p_institucion_id, p_fecha_nacimiento, p_rne, p_codigo_interno
  ) returning id into v_alumno_id;
  return v_alumno_id;
end;
$$;

create or replace function public.crear_alumno_para_persona(
  p_persona_id uuid,
  p_institucion_id uuid,
  p_fecha_nacimiento date default null,
  p_rne text default null,
  p_codigo_interno text default null
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare v_alumno_id uuid;
begin
  if not exists (select 1 from public.personas where id = p_persona_id and estado = 'activo') then
    raise exception 'La persona no existe o esta inactiva.' using errcode = '23503';
  end if;
  if not exists (select 1 from public.instituciones where id = p_institucion_id and activo) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;
  insert into public.alumnos (
    persona_id, institucion_id, fecha_nacimiento, rne, codigo_interno
  ) values (
    p_persona_id, p_institucion_id, p_fecha_nacimiento, p_rne, p_codigo_interno
  ) returning id into v_alumno_id;
  return v_alumno_id;
end;
$$;

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

-- Creacion atomica de Persona + Alumno con documento civil normalizado.
create or replace function public.crear_alumno_nueva_persona_con_documento(
  p_institucion_id uuid, p_nombres text, p_apellidos text,
  p_tipo_identificacion text, p_numero_identificacion text,
  p_fecha_nacimiento date default null, p_rne text default null,
  p_codigo_interno text default null
)
returns uuid language plpgsql security invoker
set search_path = pg_catalog, public
as $$
declare v_persona_id uuid; v_alumno_id uuid; v_documento_normalizado text;
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
  p_institucion_id uuid, p_nombres text, p_apellidos text,
  p_tipo_identificacion text, p_numero_identificacion text,
  p_fecha_nacimiento date default null, p_rne text default null,
  p_codigo_interno text default null
)
returns uuid language plpgsql security definer
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

-- Configuracion singleton de esta implementacion.
create table public.configuracion_implementacion (
  id smallint primary key default 1,
  multiples_instituciones boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint ck_configuracion_implementacion_singleton check (id = 1)
);
insert into public.configuracion_implementacion (id) values (1);

insert into public.permisos (codigo, modulo, nombre)
values
  ('configuracion.sistema.ver', 'configuracion', 'Ver configuracion del sistema'),
  ('configuracion.sistema.editar', 'configuracion', 'Editar configuracion del sistema'),
  ('configuracion.instituciones.ver', 'configuracion', 'Ver instituciones'),
  ('configuracion.instituciones.editar', 'configuracion', 'Editar instituciones')
on conflict (codigo) do nothing;
insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id from public.roles r cross join public.permisos p
where r.codigo = 'admin' and p.codigo like 'configuracion.%'
on conflict do nothing;

create or replace function public.resolver_contexto_institucional()
returns table (multiples_instituciones boolean, institucion_id uuid, institucion_nombre text)
language plpgsql security invoker set search_path = pg_catalog, public
as $$
declare v_multi boolean; v_cantidad integer;
begin
  select c.multiples_instituciones into v_multi
  from public.configuracion_implementacion c where c.id = 1;
  if not found then
    raise exception 'CONFIGURATION_REQUIRED: falta la configuracion de implementacion.' using errcode = 'SM001';
  end if;
  if v_multi then
    return query select true, null::uuid, null::text;
    return;
  end if;
  select count(*)::integer into v_cantidad from public.instituciones i where i.activo;
  if v_cantidad = 0 then
    raise exception 'NO_INSTITUTION_CONFIGURED: no hay una institucion activa.' using errcode = 'SM001';
  end if;
  if v_cantidad > 1 then
    raise exception 'MULTIPLE_INSTITUTIONS_IN_SINGLE_MODE: hay varias instituciones activas.' using errcode = 'SM002';
  end if;
  return query select false, i.id, i.nombre from public.instituciones i where i.activo;
end;
$$;

create or replace function public.rpc_obtener_contexto_implementacion()
returns jsonb language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_contexto record;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  select * into v_contexto from public.resolver_contexto_institucional();
  return jsonb_build_object(
    'multiplesInstituciones', v_contexto.multiples_instituciones,
    'institucion', case when v_contexto.institucion_id is null then null
      else jsonb_build_object('id', v_contexto.institucion_id, 'nombre', v_contexto.institucion_nombre) end
  );
end;
$$;

create or replace function public.rpc_actualizar_multiples_instituciones(p_multiples_instituciones boolean)
returns jsonb language plpgsql security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare v_activas integer;
begin
  if public.usuario_actual_id() is null
     or not public.usuario_tiene_permiso_actual('configuracion.sistema.editar', null) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if not p_multiples_instituciones then
    select count(*)::integer into v_activas from public.instituciones where activo;
    if v_activas > 1 then
      raise exception 'MULTIPLE_INSTITUTIONS_IN_SINGLE_MODE: desactive instituciones antes de usar modo single.'
        using errcode = 'SM002';
    end if;
  end if;
  update public.configuracion_implementacion
  set multiples_instituciones = p_multiples_instituciones, updated_at = now()
  where id = 1;
  return public.rpc_obtener_contexto_implementacion();
end;
$$;

alter table public.configuracion_implementacion enable row level security;
revoke all privileges on public.configuracion_implementacion from public, anon, authenticated;
grant all privileges on public.configuracion_implementacion to service_role;
revoke execute on function public.resolver_contexto_institucional() from public, anon, authenticated;
revoke execute on function public.rpc_obtener_contexto_implementacion() from public, anon;
revoke execute on function public.rpc_actualizar_multiples_instituciones(boolean) from public, anon;
grant execute on function public.rpc_obtener_contexto_implementacion() to authenticated;
grant execute on function public.rpc_actualizar_multiples_instituciones(boolean) to authenticated;
grant execute on function public.resolver_contexto_institucional() to service_role;
grant execute on function public.rpc_obtener_contexto_implementacion() to service_role;
grant execute on function public.rpc_actualizar_multiples_instituciones(boolean) to service_role;

insert into public.schema_migrations (version, nombre, checksum)
values ('baseline-001-fase1a', 'schoolmanager_fase1c_consolidado', null);

commit;

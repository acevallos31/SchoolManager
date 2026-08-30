-- SchoolManager - Baseline limpio Fase 1A.
-- Solo para instalaciones nuevas. No incluye compatibilidad legacy, RLS,
-- finanzas, ofertas academicas, triggers ni funciones.

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
  rol text not null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
  constraint ck_usuarios_rol check (rol in ('admin', 'operador', 'usuario', 'padre'))
);

create table public.alumnos (
  id uuid primary key default gen_random_uuid(),
  persona_id uuid not null references public.personas(id),
  institucion_id uuid not null references public.instituciones(id),
  rne text null,
  codigo_interno text null,
  fecha_nacimiento date null,
  estado text not null default 'activo',
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
  constraint ck_alumnos_estado check (estado in ('activo', 'inactivo'))
);

create table public.ciclos_escolares (
  id uuid primary key default gen_random_uuid(),
  institucion_id uuid not null references public.instituciones(id),
  nombre text not null,
  fecha_inicio date null,
  fecha_fin date null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
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
  grado_id uuid not null references public.grados(id),
  jornada_id uuid null references public.jornadas(id),
  nombre text not null,
  cupo integer null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
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
  ciclo_id uuid not null references public.ciclos_escolares(id),
  nombre text not null,
  tipo text null,
  fecha_inicio date not null,
  fecha_fin date not null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint uq_periodos_matricula_ciclo_nombre unique (ciclo_id, nombre),
  constraint ck_periodos_matricula_nombre_no_vacio check (btrim(nombre) <> ''),
  constraint ck_periodos_matricula_fechas check (fecha_inicio <= fecha_fin)
);

create table public.matriculas (
  id uuid primary key default gen_random_uuid(),
  alumno_id uuid not null references public.alumnos(id),
  ciclo_id uuid not null references public.ciclos_escolares(id),
  grado_id uuid not null references public.grados(id),
  seccion_id uuid not null references public.secciones(id),
  periodo_matricula_id uuid not null references public.periodos_matricula(id),
  registrado_por uuid null references public.usuarios(id),
  fecha_matricula date not null default current_date,
  estado text not null default 'pendiente',
  fecha_anulacion timestamptz null,
  motivo_anulacion text null,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint uq_matriculas_alumno_ciclo unique (alumno_id, ciclo_id),
  constraint ck_matriculas_anulacion_con_motivo check (
    fecha_anulacion is null
    or (motivo_anulacion is not null and btrim(motivo_anulacion) <> '')
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

create unique index ux_alumnos_rne_global on public.alumnos (rne)
  where rne is not null;
create unique index ux_alumnos_codigo_interno_por_institucion
  on public.alumnos (institucion_id, lower(codigo_interno))
  where codigo_interno is not null;
create unique index ux_alumnos_persona_institucion
  on public.alumnos (persona_id, institucion_id);

create unique index ux_secciones_grado_jornada_nombre
  on public.secciones (grado_id, jornada_id, lower(nombre))
  where jornada_id is not null;
create unique index ux_secciones_grado_sin_jornada_nombre
  on public.secciones (grado_id, lower(nombre))
  where jornada_id is null;

create unique index ux_alumno_responsable_principal_activo
  on public.alumno_responsable (alumno_id)
  where es_principal = true and estado = 'activo';

create index ix_alumnos_institucion_id on public.alumnos (institucion_id);
create index ix_ciclos_escolares_institucion_id on public.ciclos_escolares (institucion_id);
create index ix_secciones_grado_id on public.secciones (grado_id);
create index ix_secciones_jornada_id on public.secciones (jornada_id);
create index ix_alumno_responsable_responsable_alumno
  on public.alumno_responsable (responsable_id, alumno_id);
create index ix_alumno_responsable_alumno_estado
  on public.alumno_responsable (alumno_id, estado);
create index ix_periodos_matricula_ciclo_vigencia
  on public.periodos_matricula (ciclo_id, activo, fecha_inicio, fecha_fin);
create index ix_matriculas_ciclo_id on public.matriculas (ciclo_id);
create index ix_matriculas_grado_id on public.matriculas (grado_id);
create index ix_matriculas_seccion_id on public.matriculas (seccion_id);
create index ix_matriculas_periodo_matricula_id on public.matriculas (periodo_matricula_id);

comment on table public.configuracion_identificadores is
  'La configuracion decide obligatoriedad de identificadores, nunca PK o FK.';
comment on column public.personas.pais_emisor is
  'Nullable; una regla futura podra requerirlo segun tipo documental.';
comment on column public.alumnos.rne is
  'Identificador educativo nullable y unico global cuando existe; comparacion exacta.';

insert into public.schema_migrations (version, nombre, checksum)
values ('baseline-001-fase1a', 'schoolmanager_fase1a', null);

commit;
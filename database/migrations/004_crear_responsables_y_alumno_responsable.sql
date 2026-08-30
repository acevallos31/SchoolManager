-- Fase 1A. Objetivo: modelar responsables y su relacion N:M con alumnos.
-- Dependencias: 003_crear_personas_y_extender_alumnos_usuarios.
-- No convierte el texto legacy padres_encargados en relaciones estructuradas.

create extension if not exists pgcrypto;

create table if not exists public.responsables (
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

create table if not exists public.alumno_responsable (
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

-- Invariante: puede haber varios responsables activos, pero un solo principal activo.
create unique index if not exists ux_alumno_responsable_principal_activo
  on public.alumno_responsable (alumno_id)
  where es_principal = true and estado = 'activo';

create index if not exists ix_alumno_responsable_responsable_alumno
  on public.alumno_responsable (responsable_id, alumno_id);

create index if not exists ix_alumno_responsable_alumno_estado
  on public.alumno_responsable (alumno_id, estado);

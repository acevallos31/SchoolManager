-- Infraestructura exclusiva de pruebas y NO es una fuente canonica del esquema de produccion.
-- Representa solo el contrato legacy minimo requerido por las migraciones 001 a 006.

create extension if not exists pgcrypto;

create table public.usuarios (
  id uuid primary key default gen_random_uuid(),
  usuario text null,
  nombre text null,
  nombre_completo text null,
  correo text null,
  supabase_uid uuid null,
  rol text not null default 'usuario',
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null
);

create table public.alumnos (
  id uuid primary key default gen_random_uuid(),
  nombres text not null,
  apellidos text not null,
  dni text not null unique,
  estado text not null default 'activo',
  tutor_id uuid null references public.usuarios(id),
  padres_encargados text null,
  created_at timestamptz not null default now(),
  updated_at timestamptz null
);

create table public.ciclos_escolares (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  fecha_inicio date null,
  fecha_fin date null,
  activo boolean not null default true,
  matricula_inicio date null,
  matricula_fin date null,
  created_at timestamptz not null default now(),
  updated_at timestamptz null
);

create table public.grados (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  activo boolean not null default true
);

create table public.jornadas (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  activo boolean not null default true
);

create table public.secciones (
  id uuid primary key default gen_random_uuid(),
  grado_id uuid null references public.grados(id),
  jornada_id uuid null references public.jornadas(id),
  nombre text not null,
  activo boolean not null default true
);

create table public.matriculas (
  id uuid primary key default gen_random_uuid(),
  alumno_id uuid not null references public.alumnos(id),
  ciclo_id uuid not null references public.ciclos_escolares(id),
  grado_id uuid not null references public.grados(id),
  seccion_id uuid not null references public.secciones(id),
  fecha_matricula date not null default current_date,
  monto numeric(12, 2) not null default 0,
  estado text not null default 'pendiente',
  registrado_por uuid null references public.usuarios(id),
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  unique (alumno_id, ciclo_id)
);
create extension if not exists pgcrypto;

create table if not exists public.alumnos (
  id uuid primary key default gen_random_uuid(),
  nombres text not null,
  apellidos text not null,
  sexo text not null check (sexo in ('M', 'F', 'O')),
  dni text not null unique,
  fecha_nacimiento date not null,
  padres_encargados text not null,
  direccion text not null,
  estado text not null default 'activo' check (estado in ('activo', 'inactivo')),
  created_at timestamptz not null default now(),
  updated_at timestamptz
);

create table if not exists public.grados (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  orden integer not null,
  activo boolean not null default true
);

create table if not exists public.secciones (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  activo boolean not null default true
);

create table if not exists public.ciclos_escolares (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  fecha_inicio date,
  fecha_fin date,
  activo boolean not null default true
);

create table if not exists public.matriculas (
  id uuid primary key default gen_random_uuid(),
  alumno_id uuid not null references public.alumnos(id),
  ciclo_id uuid not null references public.ciclos_escolares(id),
  grado_id uuid not null references public.grados(id),
  seccion_id uuid not null references public.secciones(id),
  fecha_matricula date not null default current_date,
  monto numeric(12, 2) not null check (monto > 0),
  estado text not null default 'pendiente' check (estado in ('pendiente', 'pagada', 'anulada')),
  created_at timestamptz not null default now(),
  updated_at timestamptz,
  unique (alumno_id, ciclo_id)
);

create index if not exists ix_alumnos_estado on public.alumnos (estado);
create index if not exists ix_alumnos_dni on public.alumnos (dni);
create index if not exists ix_matriculas_alumno on public.matriculas (alumno_id);
create index if not exists ix_matriculas_ciclo on public.matriculas (ciclo_id);

insert into public.grados (nombre, orden)
values
  ('1er Grado', 1),
  ('2do Grado', 2),
  ('3er Grado', 3),
  ('4to Grado', 4),
  ('5to Grado', 5),
  ('6to Grado', 6),
  ('7mo Grado', 7),
  ('8vo Grado', 8),
  ('9no Grado', 9)
on conflict (nombre) do nothing;

insert into public.secciones (nombre)
values ('A'), ('B'), ('C')
on conflict (nombre) do nothing;

insert into public.ciclos_escolares (nombre, activo)
values ('2026', true)
on conflict (nombre) do nothing;

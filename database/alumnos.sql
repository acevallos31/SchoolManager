create extension if not exists pgcrypto;

create table if not exists public.alumnos (
  id uuid primary key default gen_random_uuid(),
  nombres text not null,
  apellidos text not null,
  edad integer not null check (edad >= 0 and edad <= 120),
  sexo text not null check (sexo in ('M', 'F', 'O')),
  dni text not null unique,
  padres_encargados text not null,
  direccion text not null,
  grado text,
  seccion text,
  estado text not null default 'activo' check (estado in ('activo', 'inactivo')),
  created_at timestamptz not null default now(),
  updated_at timestamptz
);

create index if not exists ix_alumnos_estado on public.alumnos (estado);
create index if not exists ix_alumnos_grado on public.alumnos (grado);
create index if not exists ix_alumnos_dni on public.alumnos (dni);


-- FUTURA / POSPUESTA. Referencia para una fase con oferta anual configurable.
-- No forma parte de la secuencia ejecutable de Fase 1A.

create extension if not exists pgcrypto;

create table if not exists public.ofertas_academicas (
  id uuid primary key default gen_random_uuid(),
  ciclo_id uuid not null references public.ciclos_escolares(id),
  grado_id uuid not null references public.grados(id),
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint uq_ofertas_academicas_ciclo_grado unique (ciclo_id, grado_id)
);

create table if not exists public.secciones_ciclo (
  id uuid primary key default gen_random_uuid(),
  oferta_academica_id uuid not null references public.ofertas_academicas(id),
  seccion_id uuid not null references public.secciones(id),
  jornada_id uuid null references public.jornadas(id),
  cupo integer null,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint ck_secciones_ciclo_cupo check (cupo is null or cupo > 0)
);

create unique index if not exists ux_secciones_ciclo_sin_jornada
  on public.secciones_ciclo (oferta_academica_id, seccion_id)
  where jornada_id is null;

create unique index if not exists ux_secciones_ciclo_con_jornada
  on public.secciones_ciclo (oferta_academica_id, seccion_id, jornada_id)
  where jornada_id is not null;

create index if not exists ix_ofertas_academicas_ciclo_id
  on public.ofertas_academicas (ciclo_id);

create index if not exists ix_secciones_ciclo_oferta_id
  on public.secciones_ciclo (oferta_academica_id);

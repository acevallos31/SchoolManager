-- Fase 1A. Objetivo: asociar ciclos a institucion y crear periodos configurables.
-- Dependencias: 002_crear_instituciones_y_configuracion_identificadores.
-- matricula_inicio y matricula_fin legacy se mantienen sin cambios.

create extension if not exists pgcrypto;

alter table public.ciclos_escolares add column if not exists institucion_id uuid null
  references public.instituciones(id);
alter table public.ciclos_escolares add column if not exists fecha_desactivacion timestamptz null;
alter table public.ciclos_escolares add column if not exists motivo_desactivacion text null;

create index if not exists ix_ciclos_escolares_institucion_id
  on public.ciclos_escolares (institucion_id);

create table if not exists public.periodos_matricula (
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

create index if not exists ix_periodos_matricula_ciclo_vigencia
  on public.periodos_matricula (ciclo_id, activo, fecha_inicio, fecha_fin);

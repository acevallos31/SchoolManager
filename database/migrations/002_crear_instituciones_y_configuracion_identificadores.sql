-- Fase 1A. Objetivo: crear configuracion institucional e identificadores.
-- Dependencias: 001_establecer_convencion_migraciones.
-- No inserta una institucion predeterminada ni altera datos existentes.

create extension if not exists pgcrypto;

create table if not exists public.instituciones (
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

create table if not exists public.configuracion_identificadores (
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

comment on table public.configuracion_identificadores is
  'La configuracion decide obligatoriedad y uso de identificadores, nunca PK o FK.';

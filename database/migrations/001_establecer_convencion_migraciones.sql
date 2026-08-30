-- Fase 1A. Objetivo: registrar migraciones aplicadas sin modificar el dominio.
-- Dependencias: pgcrypto ya debe estar disponible para las migraciones posteriores.
-- No ejecutar sin la aprobacion operativa correspondiente.

create table if not exists public.schema_migrations (
  version text primary key,
  nombre text not null,
  aplicado_en timestamptz not null default now(),
  checksum text null,
  constraint schema_migrations_version_no_vacia check (btrim(version) <> ''),
  constraint schema_migrations_nombre_no_vacio check (btrim(nombre) <> '')
);

comment on table public.schema_migrations is
  'Registro de migraciones SQL aplicadas manualmente o por el ejecutor aprobado.';

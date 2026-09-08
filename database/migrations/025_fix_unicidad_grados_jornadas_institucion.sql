-- ======================================================================
-- Bloque 030E - Fix de unicidad de grados/jornadas por institucion.
-- ----------------------------------------------------------------------
-- Correccion (post-024) del contrato de unicidad de nombre que la 020 dejó
-- roto en silencio. La migración historica 020 y su baseline real usan el
-- nombre de constraint `uq_grados_nombre` / `uq_jornadas_nombre` (convencion
-- manual), pero la 020 solo intentaba retirar los auto-nombrados
-- `grados_nombre_key` / `jornadas_nombre_key` -- que en la practica NO existen
-- en el schema real. Por tanto tras la 020 conviven:
--   * la unicidad GLOBAL  uq_*        sobre (nombre);        <- stale, indebida
--   * la unicidad NUEVA   ux_*_institucion_nombre
--                          sobre (institucion_id, lower(btrim(nombre))).
-- El objetivo multiinstitución (permitir el mismo nombre en instituciones
-- distintas) quedaba incumplido en silencio.
--
-- Esta tambien valida que "el mismo nombre en instituciones distintas"
-- siga siendo rechazado segun el contrato por institucion.
--
-- OJO (no se toca la 020 por politica): la 020 ya versionada queda intacta su
-- logica; esta 025 la corrige al CERRAR el contrato correcto. En entornos
-- nuevos (020 ya arreglada) sus crea/drop son no-op idempotentes.
--
-- Idempotente (patron 023/024) y segura de re-aplicar.
-- ======================================================================
begin;

-- Precedencia explicita: requiere la 024.
do $$ begin
  if not exists (select 1 from public.schema_migrations where version = '024') then
    raise exception 'Migracion 025 requiere la migracion 024 (rbac_permisos_aplicacion_estructura).';
  end if;
end $$;

-- ============ 1. RETIRAR LA UNICIDAD GLOBAL SOBRE NOMBRE ============
-- Ambos contratos historicos, idempotente: cualquiera que exista se retira;
-- los que nunca existieron son no-op inofensivos.
alter table public.grados   drop constraint if exists grados_nombre_key;
alter table public.grados   drop constraint if exists uq_grados_nombre;
alter table public.jornadas drop constraint if exists jornadas_nombre_key;
alter table public.jornadas drop constraint if exists uq_jornadas_nombre;

-- ============ 2. CONFIRMAR LA UNICIDAD POR INSTITUCION ============
-- Los índices únicos por (institucion_id, lower(btrim(nombre))) fueron creados
-- por la 020. Se confirman de forma idempotente: si existieran (entorno ya
-- migrado con 020 correcta) es no-op; si por cualquier razon no, se crean para
-- cerrar el contrato correcto.
create unique index if not exists ux_grados_institucion_nombre
  on public.grados(institucion_id, lower(btrim(nombre)));
create unique index if not exists ux_jornadas_institucion_nombre
  on public.jornadas(institucion_id, lower(btrim(nombre)));

-- ============ 3. REGISTRO DE LA MIGRACION ============
insert into public.schema_migrations (version, nombre, checksum)
values ('025', 'fix_unicidad_grados_jornadas_institucion', null)
on conflict (version) do nothing;

commit;
-- ======================================================================
-- Rollback 025 - Fix de unicidad de grados/jornadas por institucion.
-- ----------------------------------------------------------------------
-- Revierte el efecto de la 025:
--   * re-establece la unicidad GLOBAL por nombre (contrato autocontenido
--     de la baseline real: `uq_grados_nombre` / `uq_jornadas_nombre` sobre
--     (nombre)); y
--   * retira los indices unicos POR INSTITUCION
--     (ux_grados_institucion_nombre / ux_jornadas_institucion_nombre
--      sobre (institucion_id, lower(btrim(nombre)))).
-- (El rollback de la 020 recrea los auto-nombrados *nombre_key; este 025
--  restaura el contrato AUTENTICO uq_* que la baseline realmente declara.)
--
-- ADVERTENCIA OBLIGATORIA SOBRE DUPLICADOS MULTIINSTITUCION:
--   Este rollback solo es seguro si EXISTE DATOS MULTIINSTITUCION O SE REVIERTE
--   INMEDIATAMENTE tras la 025 sin migraciones intermedias. Si en la base ya
--   se insertaron grados/jornadas con el MISMO nombre en instituciones
--   DISTINTAS (posible unicamente tras la 020/025), el `add constraint
--   uq_* unique (nombre)` del paso 1 FALLARA por violacion de unicidad global.
--   En ese caso NO intente el rollback: restaure del backup de produccion.
--   Esta advertencia es intencionada: el rollback de una correccion de
--   unicidad no es seguro con datos multiinstitución.
--
-- Idempotente y coherente con la direccion global del fixture de rollbacks
-- (se ejecuta en orden inverso a las migraciones, antes del rollback de la 020).
-- ======================================================================
begin;

-- 1. Restaurar la unicidad global por nombre (contrato baseline real uq_*).
-- Nota: ALTER TABLE ... ADD CONSTRAINT IF NOT EXISTS no existe en PostgreSQL;
-- se usa un bloque DO que comprueba pg_constraint para mantener idempotencia.
do $$
begin
  if not exists (select 1 from pg_constraint where conname='uq_grados_nombre') then
    alter table public.grados add constraint uq_grados_nombre unique (nombre);
  end if;
  if not exists (select 1 from pg_constraint where conname='uq_jornadas_nombre') then
    alter table public.jornadas add constraint uq_jornadas_nombre unique (nombre);
  end if;
end $$;

-- 2. Retirar la unicidad por institucion.
drop index if exists public.ux_grados_institucion_nombre;
drop index if exists public.ux_jornadas_institucion_nombre;

-- 3. Quitar el registro de la migracion.
delete from public.schema_migrations where version = '025';

commit;
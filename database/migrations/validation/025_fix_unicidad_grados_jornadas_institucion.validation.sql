-- ======================================================================
-- Validacion 025 - Fix de unicidad de grados/jornadas por institucion.
-- ----------------------------------------------------------------------
-- Contrato: la validacion PASSA si y solo si no devuelve filas.
-- Toda consulta que devuelva filas es un hallazgo que debe resolverse.
-- ----------------------------------------------------------------------
-- Chequea el estado INMEDIATAMENTE POSTERIOR a la 025 (no el esquema final):
-- los indices/constraints de unicidad global por nombre deben haber quedado
-- eliminados en AMBOS contratos historicos, y los indices unicos por
-- institucion deben existir.
-- ======================================================================

-- 1. La migracion quedo registrada exactamente una vez.
select '025' as migracion_no_registrada
from (select count(*) as n from public.schema_migrations where version = '025') c
where c.n = 0;

select '025' as migracion_duplicada
from public.schema_migrations
where version='025'
group by version having count(*) > 1;

-- 2. Unicidad global por nombre ELIMINADA (ambos contratos historicos).
select 'uq_grados_nombre_indebido' as error
where exists (select 1 from pg_constraint where conname='uq_grados_nombre');
select 'uq_jornadas_nombre_indebido' as error
where exists (select 1 from pg_constraint where conname='uq_jornadas_nombre');
select 'grados_nombre_key_indebido' as error
where exists (select 1 from pg_constraint where conname='grados_nombre_key');
select 'jornadas_nombre_key_indebido' as error
where exists (select 1 from pg_constraint where conname='jornadas_nombre_key');

-- 3. Unicidad POR INSTITUCION confirmada y presente.
select 'ux_grados_institucion_nombre_faltante' as error
where to_regclass('public.ux_grados_institucion_nombre') is null;
select 'ux_jornadas_institucion_nombre_faltante' as error
where to_regclass('public.ux_jornadas_institucion_nombre') is null;

-- 4. Los indices por institucion deben ser realmente UNIQUE.
select 'ux_grados_institucion_nombre_no_unico' as error
where to_regclass('public.ux_grados_institucion_nombre') is not null
  and not exists (
    select 1
    from pg_index i
    join pg_class c on c.oid = i.indexrelid
    where c.relname = 'ux_grados_institucion_nombre'
      and i.indisunique
  );
select 'ux_jornadas_institucion_nombre_no_unico' as error
where to_regclass('public.ux_jornadas_institucion_nombre') is not null
  and not exists (
    select 1
    from pg_index i
    join pg_class c on c.oid = i.indexrelid
    where c.relname = 'ux_jornadas_institucion_nombre'
      and i.indisunique
  );

-- 5. Definicion real EXACTA: clave (institucion_id, lower(btrim(nombre))).
--    Se valida contra pg_get_indexdef para exigir la expresion concreta, no
--    solo la presencia de la columna institucion_id.
select 'ux_grados_institucion_nombre_definicion_incorrecta' as error
where to_regclass('public.ux_grados_institucion_nombre') is not null
  and not exists (
    select 1
    from pg_index i
    join pg_class c on c.oid = i.indexrelid
    where c.relname = 'ux_grados_institucion_nombre'
      and pg_get_indexdef(i.indexrelid)
          like '%(institucion_id, lower(btrim(nombre)))%'
  );
select 'ux_jornadas_institucion_nombre_definicion_incorrecta' as error
where to_regclass('public.ux_jornadas_institucion_nombre') is not null
  and not exists (
    select 1
    from pg_index i
    join pg_class c on c.oid = i.indexrelid
    where c.relname = 'ux_jornadas_institucion_nombre'
      and pg_get_indexdef(i.indexrelid)
          like '%(institucion_id, lower(btrim(nombre)))%'
  );

-- 6. No quedaron indices legacy normalizados (deberian seguir retirados).
select 'ux_grados_nombre_normalizado_indebido' as error
where to_regclass('public.ux_grados_nombre_normalizado') is not null;
select 'ux_jornadas_nombre_normalizado_indebido' as error
where to_regclass('public.ux_jornadas_nombre_normalizado') is not null;
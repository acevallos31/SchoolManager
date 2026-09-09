-- Validacion 026 - cada fila devuelta es un hallazgo.

-- 1. Migracion registrada exactamente una vez.
select '026_no_registrada' as error
where not exists (
  select 1 from public.schema_migrations where version = '026'
);

select '026_duplicada' as error
from public.schema_migrations
where version = '026'
group by version
having count(*) > 1;

-- 2. La unicidad global historica ya no debe existir.
select 'uq_matriculas_alumno_ciclo_indebida' as error
where exists (
  select 1 from pg_constraint where conname = 'uq_matriculas_alumno_ciclo'
);

select 'matriculas_alumno_id_ciclo_id_key_indebida' as error
where exists (
  select 1 from pg_constraint where conname = 'matriculas_alumno_id_ciclo_id_key'
);

-- 3. Debe existir el indice unico parcial alumno+ciclo excluyendo anuladas.
select 'ux_matriculas_alumno_ciclo_no_anulada_faltante' as error
where to_regclass('public.ux_matriculas_alumno_ciclo_no_anulada') is null;

select 'ux_matriculas_alumno_ciclo_no_anulada_no_unico' as error
where to_regclass('public.ux_matriculas_alumno_ciclo_no_anulada') is not null
  and not exists (
    select 1
    from pg_index i
    join pg_class c on c.oid = i.indexrelid
    where c.relname = 'ux_matriculas_alumno_ciclo_no_anulada'
      and i.indisunique
  );

select 'ux_matriculas_alumno_ciclo_no_anulada_definicion_incorrecta' as error
where to_regclass('public.ux_matriculas_alumno_ciclo_no_anulada') is not null
  and not exists (
    select 1
    from pg_index i
    join pg_class c on c.oid = i.indexrelid
    where c.relname = 'ux_matriculas_alumno_ciclo_no_anulada'
      and pg_get_indexdef(i.indexrelid) like '%(alumno_id, ciclo_id)%'
      and pg_get_expr(i.indpred, i.indrelid) = '(estado <> ''anulada''::text)'
  );

-- 4. El listado historico por alumno conserva un indice no parcial.
select 'ix_matriculas_alumno_faltante' as error
where to_regclass('public.ix_matriculas_alumno') is null;

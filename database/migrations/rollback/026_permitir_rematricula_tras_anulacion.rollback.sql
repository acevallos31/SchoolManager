-- Rollback 026: restaura la unicidad global historica alumno+ciclo.
-- Si ya existen varias matriculas del mismo alumno+ciclo (por ejemplo una
-- anulada y una nueva), el rollback se detiene de forma explicita para no
-- perder historial ni elegir una fila arbitrariamente.

begin;

DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM public.matriculas
    GROUP BY alumno_id, ciclo_id
    HAVING count(*) > 1
  ) THEN
    RAISE EXCEPTION 'No se puede revertir 026: existen varias matriculas para el mismo alumno y ciclo.';
  END IF;
END
$$;

drop index if exists public.ux_matriculas_alumno_ciclo_no_anulada;
drop index if exists public.ix_matriculas_alumno;

alter table public.matriculas
  add constraint uq_matriculas_alumno_ciclo unique (alumno_id, ciclo_id);

delete from public.schema_migrations where version = '026';

commit;

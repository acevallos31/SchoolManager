-- ======================================================================
-- Migracion 026 - permitir una nueva matricula en el mismo ciclo cuando la
-- matricula anterior fue anulada.
--
-- La restriccion historica uq_matriculas_alumno_ciclo impedía conservar la
-- fila anulada y volver a matricular al alumno en ese ciclo. La nueva regla
-- mantiene una sola matricula NO anulada por alumno+ciclo y conserva todas
-- las anuladas como historial.
-- ======================================================================

begin;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM public.schema_migrations WHERE version = '025'
  ) THEN
    RAISE EXCEPTION 'La migracion 026 requiere la 025 aplicada previamente.';
  END IF;
END
$$;

-- Retira la unicidad global historica. Se incluye el nombre legacy por
-- tolerancia a instalaciones antiguas que hayan llegado con ese contrato.
alter table public.matriculas
  drop constraint if exists uq_matriculas_alumno_ciclo;
alter table public.matriculas
  drop constraint if exists matriculas_alumno_id_ciclo_id_key;

-- Conservamos el nombre uq_matriculas_alumno_ciclo como NOMBRE DE INDICE para
-- no romper observabilidad/tests/perf que identifican la defensa de unicidad,
-- pero deja de ser una constraint global: ahora es un indice UNIQUE parcial.
-- Una fila anulada libera el alumno+ciclo para una nueva matricula.
create unique index if not exists uq_matriculas_alumno_ciclo
  on public.matriculas (alumno_id, ciclo_id)
  where estado <> 'anulada';

-- El indice parcial no cubre filas anuladas. Este indice simple mantiene
-- eficiente el historial completo por alumno, incluido lo anulado.
create index if not exists ix_matriculas_alumno
  on public.matriculas (alumno_id);

insert into public.schema_migrations (version, nombre, checksum)
values ('026', 'permitir_rematricula_tras_anulacion', null)
on conflict (version) do nothing;

commit;

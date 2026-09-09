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

-- Una fila anulada deja de bloquear una nueva matricula del mismo alumno en el
-- mismo ciclo. Pendiente/activa/finalizada/retirada/trasladada siguen siendo
-- excluyentes para evitar dos matriculas vigentes o historicos incompatibles.
create unique index if not exists ux_matriculas_alumno_ciclo_no_anulada
  on public.matriculas (alumno_id, ciclo_id)
  where estado <> 'anulada';

-- La restriccion unica anterior tambien servia al listado historico por alumno.
-- El indice parcial no cubre las filas anuladas, por lo que se conserva un
-- indice simple para no degradar consultas que muestran todo el historial.
create index if not exists ix_matriculas_alumno
  on public.matriculas (alumno_id);

insert into public.schema_migrations (version, nombre, checksum)
values ('026', 'permitir_rematricula_tras_anulacion', null)
on conflict (version) do nothing;

commit;

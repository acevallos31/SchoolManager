-- Fase 1A. Objetivo: asociar matriculas a periodos sin cambiar el modelo academico actual.
-- Dependencias: 005_extender_ciclos_y_crear_periodos_matricula.
-- alumno_id, ciclo_id, grado_id, seccion_id y registrado_por son canonicos actuales.

alter table public.matriculas add column if not exists periodo_matricula_id uuid null
  references public.periodos_matricula(id);
alter table public.matriculas add column if not exists fecha_anulacion timestamptz null;
alter table public.matriculas add column if not exists motivo_anulacion text null;

do $$
begin
  if not exists (
    select 1 from pg_constraint where conname = 'ck_matriculas_anulacion_con_motivo'
  ) then
    alter table public.matriculas add constraint ck_matriculas_anulacion_con_motivo
      check (
        fecha_anulacion is null
        or (motivo_anulacion is not null and btrim(motivo_anulacion) <> '')
      ) not valid;
  end if;
end;
$$;

create index if not exists ix_matriculas_periodo_matricula_id
  on public.matriculas (periodo_matricula_id);

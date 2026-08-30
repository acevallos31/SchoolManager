-- Rollback 008 conservador.
-- Se bloquea ante cualquier seccion o matricula porque eliminar el contexto de
-- ciclo/institucion o el historial podria perder significado academico.
begin;

do $$
begin
  if exists (select 1 from public.matricula_estado_historial)
     or exists (select 1 from public.matriculas)
     or exists (select 1 from public.secciones)
     or exists (select 1 from public.alumnos where fecha_nacimiento is not null) then
    raise exception 'Rollback 008 bloqueado: existen datos academicos que perderian contexto.';
  end if;

  if exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'alumnos' and column_name = 'nombres'
  ) and exists (select 1 from public.alumnos where nombres is null or apellidos is null or dni is null) then
    raise exception 'Rollback 008 bloqueado: existen alumnos normalizados sin columnas legacy.';
  end if;
end;
$$;

drop trigger if exists trg_matriculas_historial_estado on public.matriculas;
drop function if exists public.registrar_historial_estado_matricula();
drop function if exists public.crear_seccion(uuid, uuid, uuid, uuid, text, integer);
drop function if exists public.matricular_alumno(uuid, uuid, uuid, uuid);
drop function if exists public.cambiar_estado_matricula(uuid, text, uuid, text);
drop function if exists public.desactivar_alumno(uuid, uuid, text);
drop function if exists public.reactivar_alumno(uuid, uuid);
drop function if exists public.desactivar_seccion(uuid, text);
drop function if exists public.crear_alumno_nueva_persona(uuid, text, text, date, text, text);
drop function if exists public.crear_alumno_para_persona(uuid, uuid, date, text, text);
drop table if exists public.matricula_estado_historial;

alter table public.matriculas drop constraint if exists fk_matriculas_alumno_institucion;
alter table public.matriculas drop constraint if exists fk_matriculas_seccion_contexto;
alter table public.matriculas drop constraint if exists fk_matriculas_periodo_ciclo;
alter table public.matriculas drop constraint if exists fk_matriculas_registrado_por;
alter table public.matriculas drop constraint if exists uq_matriculas_alumno_ciclo;
alter table public.matriculas drop constraint if exists ck_matriculas_estado;
alter table public.matriculas drop constraint if exists ck_matriculas_anulacion_coherente;
drop index if exists public.ix_matriculas_institucion_ciclo_estado;

alter table public.matriculas add column grado_id uuid null;
alter table public.matriculas alter column grado_id set not null;
alter table public.matriculas alter column periodo_matricula_id drop not null;
alter table public.matriculas drop column institucion_id;
alter table public.matriculas drop constraint if exists ck_matriculas_anulacion_con_motivo;
alter table public.matriculas
  add constraint matriculas_alumno_id_fkey foreign key (alumno_id)
    references public.alumnos(id),
  add constraint matriculas_ciclo_id_fkey foreign key (ciclo_id)
    references public.ciclos_escolares(id),
  add constraint matriculas_grado_id_fkey foreign key (grado_id)
    references public.grados(id),
  add constraint matriculas_seccion_id_fkey foreign key (seccion_id)
    references public.secciones(id),
  add constraint matriculas_periodo_matricula_id_fkey foreign key (periodo_matricula_id)
    references public.periodos_matricula(id),
  add constraint matriculas_registrado_por_fkey foreign key (registrado_por)
    references public.usuarios(id),
  add constraint uq_matriculas_alumno_ciclo unique (alumno_id, ciclo_id),
  add constraint ck_matriculas_anulacion_con_motivo check (
    fecha_anulacion is null
    or (motivo_anulacion is not null and btrim(motivo_anulacion) <> '')
  );
create index ix_matriculas_grado_id on public.matriculas (grado_id);

alter table public.secciones drop constraint if exists fk_secciones_ciclo_institucion;
alter table public.secciones drop constraint if exists fk_secciones_grado;
alter table public.secciones drop constraint if exists fk_secciones_jornada;
drop index if exists public.ux_secciones_contexto_jornada_nombre;
drop index if exists public.ux_secciones_contexto_sin_jornada_nombre;
drop index if exists public.ix_secciones_ciclo_grado;
drop index if exists public.ix_secciones_institucion_activo;
alter table public.secciones
  add constraint secciones_grado_id_fkey foreign key (grado_id) references public.grados(id),
  add constraint secciones_jornada_id_fkey foreign key (jornada_id) references public.jornadas(id);
alter table public.secciones alter column grado_id drop not null;
alter table public.secciones drop column institucion_id;
alter table public.secciones drop column ciclo_id;
alter table public.secciones drop column cupo;
alter table public.secciones drop column fecha_desactivacion;
alter table public.secciones drop column motivo_desactivacion;

alter table public.periodos_matricula drop constraint if exists uq_periodos_id_ciclo;
alter table public.alumnos drop constraint if exists uq_alumnos_id_institucion;
alter table public.ciclos_escolares drop constraint if exists uq_ciclos_id_institucion;
alter table public.alumnos alter column institucion_id drop not null;
alter table public.ciclos_escolares alter column institucion_id drop not null;
alter table public.alumnos drop column fecha_nacimiento;

do $$
begin
  if exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'alumnos' and column_name = 'nombres'
  ) then
    alter table public.alumnos alter column nombres set not null;
    alter table public.alumnos alter column apellidos set not null;
    alter table public.alumnos alter column dni set not null;
  end if;
end;
$$;

delete from public.schema_migrations where version = '008';

commit;

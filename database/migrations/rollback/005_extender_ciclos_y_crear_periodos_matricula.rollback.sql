-- Rollback 005. Ejecutar despues de revertir 007 y solo sin datos nuevos.
do $$
begin
  if exists (select 1 from public.periodos_matricula)
  or exists (
    select 1 from public.ciclos_escolares
    where institucion_id is not null
    or fecha_desactivacion is not null
    or motivo_desactivacion is not null
  ) then
    raise exception 'Rollback bloqueado: existen periodos o ciclos asociados.';
  end if;
end;
$$;
drop index if exists public.ix_ciclos_escolares_institucion_id;
drop table if exists public.periodos_matricula;
alter table public.ciclos_escolares drop column if exists institucion_id;
alter table public.ciclos_escolares drop column if exists fecha_desactivacion;
alter table public.ciclos_escolares drop column if exists motivo_desactivacion;

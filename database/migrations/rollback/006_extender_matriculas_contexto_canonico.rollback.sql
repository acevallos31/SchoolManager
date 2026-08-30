-- Rollback 006. Solo es seguro antes de poblar las columnas nuevas de matriculas.
do $$
begin
  if exists (
    select 1 from public.matriculas
    where periodo_matricula_id is not null
       or fecha_anulacion is not null
       or motivo_anulacion is not null
  ) then
    raise exception 'Rollback bloqueado: matriculas contienen datos canonicos nuevos.';
  end if;
end;
$$;
alter table public.matriculas drop constraint if exists ck_matriculas_anulacion_con_motivo;
drop index if exists public.ix_matriculas_periodo_matricula_id;
alter table public.matriculas drop column if exists periodo_matricula_id;
alter table public.matriculas drop column if exists fecha_anulacion;
alter table public.matriculas drop column if exists motivo_anulacion;

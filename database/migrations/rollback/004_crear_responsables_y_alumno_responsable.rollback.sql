-- Rollback 004. Solo es seguro si las tablas nuevas estan vacias.
do $$
begin
  if exists (select 1 from public.alumno_responsable)
     or exists (select 1 from public.responsables) then
    raise exception 'Rollback bloqueado: existen responsables o relaciones.';
  end if;
end;
$$;
drop table if exists public.alumno_responsable;
drop table if exists public.responsables;

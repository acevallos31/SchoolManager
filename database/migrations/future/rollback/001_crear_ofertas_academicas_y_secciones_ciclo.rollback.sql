-- Rollback de migracion FUTURA / POSPUESTA. Solo ejecutar si ambas tablas estan vacias.
do $$
begin
  if exists (select 1 from public.secciones_ciclo)
     or exists (select 1 from public.ofertas_academicas) then
    raise exception 'Rollback bloqueado: existen ofertas o secciones por ciclo.';
  end if;
end;
$$;
drop table if exists public.secciones_ciclo;
drop table if exists public.ofertas_academicas;

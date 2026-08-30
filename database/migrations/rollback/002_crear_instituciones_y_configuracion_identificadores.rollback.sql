-- Rollback 002. Ejecutar antes de rollbacks dependientes y solo sin datos.
do $$
begin
  if exists (select 1 from public.configuracion_identificadores)
     or exists (select 1 from public.instituciones) then
    raise exception 'Rollback bloqueado: existen instituciones o configuraciones.';
  end if;
end;
$$;
drop table if exists public.configuracion_identificadores;
drop table if exists public.instituciones;

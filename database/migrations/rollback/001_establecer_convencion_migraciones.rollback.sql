-- Rollback 001. Ejecutar solo si schema_migrations esta vacia.
do $$
begin
  if exists (select 1 from public.schema_migrations) then
    raise exception 'Rollback bloqueado: schema_migrations contiene registros.';
  end if;
end;
$$;
drop table if exists public.schema_migrations;

select '033 no registrado' as error
where not exists (select 1 from public.schema_migrations where version='033');

select 'rpc_asignar_rol_institucional no existe' as error
where to_regprocedure('public.rpc_asignar_rol_institucional(uuid,uuid)') is null;

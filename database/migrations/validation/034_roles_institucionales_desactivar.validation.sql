select '034 no registrado' as error
where not exists (select 1 from public.schema_migrations where version='034');

select 'rpc_desactivar_rol_institucional no existe' as error
where to_regprocedure('public.rpc_desactivar_rol_institucional(uuid,text)') is null;

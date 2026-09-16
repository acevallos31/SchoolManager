-- Filas devueltas = hallazgos.
select '029 no registrado' as error
where not exists (select 1 from public.schema_migrations where version='029');

select 'seguridad_auditoria no existe' as error
where to_regclass('public.seguridad_auditoria') is null;

select 'rpc_crear_rol_institucional no existe' as error
where to_regprocedure('public.rpc_crear_rol_institucional(uuid,text,text,text)') is null;

select 'usuario_es_admin_institucional no existe' as error
where to_regprocedure('public.usuario_es_admin_institucional(uuid,uuid)') is null;

select 'hay roles institucionales con codigo platform_admin' as error
where exists (
  select 1 from public.roles
  where tipo='institucional' and codigo='platform_admin'
);

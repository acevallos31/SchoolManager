-- Filas devueltas = hallazgos.
select '032 no registrado' as error
where not exists (select 1 from public.schema_migrations where version='032');

select 'rpc_reemplazar_permisos_rol_institucional no existe' as error
where to_regprocedure('public.rpc_reemplazar_permisos_rol_institucional(uuid,text[])') is null;

select 'rol institucional contiene permiso fuera de cota' as error
where exists (
  select 1
  from public.roles r
  join public.roles_permisos rp on rp.rol_id=r.id
  join public.permisos p on p.id=rp.permiso_id
  where r.tipo='institucional'
    and (p.ambito<>'institucion' or not p.delegable or p.estado<>'vigente')
);

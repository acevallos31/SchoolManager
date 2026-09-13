-- Filas devueltas = hallazgos.
select '030 no registrado' as error
where not exists (select 1 from public.schema_migrations where version='030');

select 'rpc_clonar_plantilla_rol no existe' as error
where to_regprocedure('public.rpc_clonar_plantilla_rol(uuid,text,text,text,text)') is null;

select 'rpc_editar_rol_institucional no existe' as error
where to_regprocedure('public.rpc_editar_rol_institucional(uuid,text,text)') is null;

select 'rol institucional contiene permiso de plataforma o no delegable' as error
where exists (
  select 1
  from public.roles r
  join public.roles_permisos rp on rp.rol_id=r.id
  join public.permisos p on p.id=rp.permiso_id
  where r.tipo='institucional'
    and (p.ambito<>'institucion' or not p.delegable)
);

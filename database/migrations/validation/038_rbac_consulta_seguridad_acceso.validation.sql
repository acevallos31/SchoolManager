select '038 no registrado' as error
where not exists (
  select 1 from public.schema_migrations where version = '038'
);

select 'rpc_obtener_seguridad_acceso no existe' as error
where to_regprocedure('public.rpc_obtener_seguridad_acceso(uuid)') is null;

select 'authenticated no puede ejecutar rpc_obtener_seguridad_acceso' as error
where not has_function_privilege(
  'authenticated',
  'public.rpc_obtener_seguridad_acceso(uuid)',
  'EXECUTE'
);

select 'consulta Seguridad y acceso no usa autoridad institucional estricta' as error
where position(
  'usuario_tiene_permiso_institucional_estricto'
  in pg_get_functiondef('public.rpc_obtener_seguridad_acceso(uuid)'::regprocedure)
) = 0;

select 'consulta Seguridad y acceso no filtra asignaciones por institucion' as error
where position(
  'ur.institucion_id = p_institucion_id'
  in pg_get_functiondef('public.rpc_obtener_seguridad_acceso(uuid)'::regprocedure)
) = 0;

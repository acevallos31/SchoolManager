-- Las consultas de diagnostico deben devolver cero filas, salvo el registro final.
select 'configuracion singleton invalida' as error
where (select count(*) from public.configuracion_implementacion) <> 1
   or not exists (
     select 1 from public.configuracion_implementacion
     where id = 1 and multiples_instituciones = false
   );

select 'funcion de contexto faltante' as error
where to_regprocedure('public.resolver_contexto_institucional()') is null;

select 'RPC de lectura faltante' as error
where to_regprocedure('public.rpc_obtener_contexto_implementacion()') is null;

select 'RPC de actualizacion faltante' as error
where to_regprocedure('public.rpc_actualizar_multiples_instituciones(boolean)') is null;

select codigo as permiso_faltante
from (values
  ('configuracion.sistema.ver'),
  ('configuracion.sistema.editar'),
  ('configuracion.instituciones.ver'),
  ('configuracion.instituciones.editar')
) esperado(codigo)
where not exists (select 1 from public.permisos p where p.codigo = esperado.codigo);

select version, nombre from public.schema_migrations where version = '012';

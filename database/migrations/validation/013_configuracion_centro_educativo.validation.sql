-- Las consultas de diagnostico deben devolver cero filas, salvo el registro final.
select 'RPC lectura faltante' as error
where to_regprocedure('public.rpc_obtener_configuracion_institucion(uuid)') is null;

select 'RPC creacion faltante' as error
where to_regprocedure(
  'public.rpc_crear_institucion(text,text,text,text,text,text,boolean,boolean,boolean,text[])'
) is null;

select 'RPC actualizacion faltante' as error
where to_regprocedure(
  'public.rpc_actualizar_institucion(uuid,text,text,text,text,text,text,boolean,boolean,boolean,text[])'
) is null;

select esperado.tabla as rls_faltante
from (values ('instituciones'), ('configuracion_identificadores')) esperado(tabla)
join pg_class c on c.relname = esperado.tabla and c.relnamespace = 'public'::regnamespace
where not c.relrowsecurity;

select institucion_id, count(*)
from public.configuracion_identificadores
group by institucion_id having count(*) > 1;

select version, nombre from public.schema_migrations where version = '013';

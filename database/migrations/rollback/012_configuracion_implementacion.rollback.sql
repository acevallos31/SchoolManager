begin;

drop function if exists public.rpc_actualizar_multiples_instituciones(boolean);
drop function if exists public.rpc_obtener_contexto_implementacion();
drop function if exists public.resolver_contexto_institucional();

delete from public.roles_permisos rp
using public.permisos p
where rp.permiso_id = p.id and p.codigo like 'configuracion.%';
delete from public.permisos where codigo like 'configuracion.%';

drop table if exists public.configuracion_implementacion;
delete from public.schema_migrations where version = '012';

commit;

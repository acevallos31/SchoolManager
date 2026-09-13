begin;

drop function if exists public.rpc_obtener_seguridad_acceso(uuid);

delete from public.schema_migrations where version = '038';

commit;

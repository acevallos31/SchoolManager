begin;

drop function if exists public.rpc_asignar_rol_institucional(uuid,uuid);
delete from public.schema_migrations where version='033';

commit;

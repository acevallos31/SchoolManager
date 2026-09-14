begin;

drop function if exists public.rpc_reemplazar_permisos_rol_institucional(uuid,text[]);
delete from public.schema_migrations where version='032';

commit;

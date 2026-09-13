begin;

drop function if exists public.rpc_desactivar_rol_institucional(uuid,text);
delete from public.schema_migrations where version='034';

commit;

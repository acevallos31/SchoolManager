begin;

drop function if exists public.rpc_crear_rol_institucional(uuid,text,text,text);
drop function if exists public.usuario_es_admin_institucional(uuid,uuid);
drop table if exists public.seguridad_auditoria;
delete from public.schema_migrations where version='029';

commit;

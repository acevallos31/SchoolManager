begin;

drop function if exists public.rpc_editar_rol_institucional(uuid,text,text);
drop function if exists public.rpc_clonar_plantilla_rol(uuid,text,text,text,text);
delete from public.schema_migrations where version='030';

commit;

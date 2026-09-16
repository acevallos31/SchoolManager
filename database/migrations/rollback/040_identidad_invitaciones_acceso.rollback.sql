begin;

drop function if exists public.rpc_preparar_invitacion_usuario(uuid,text,text,text,uuid,text);
drop table if exists public.invitaciones_acceso;

-- No se restaura NOT NULL sobre usuarios.auth_user_id porque el estado previo
-- puede contener usuarios pendientes creados durante la vigencia de 040. Esa
-- reversión requiere primero una verificación explícita de datos.

delete from public.schema_migrations where version = '040';

commit;

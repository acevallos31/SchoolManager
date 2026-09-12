-- Rollback 027: retira la RPC de vinculacion explicita y su registro.
--
-- IMPORTANTE: este rollback NO desvincula identidades ya escritas. La columna
-- public.usuarios.auth_user_id es dato de aplicacion en uso; borrarla
-- silenciosamente dejaria usuarios sin acceso. Para desvincular hay que hacerlo
-- de forma explicita y consciente:
--
--   update public.usuarios set auth_user_id = null where id = '<usuario_id>';
--
-- (o volver a ejecutar public.vincular_identidad_usuario con la identidad
-- correcta antes de retirar la funcion).

begin;

drop function if exists public.vincular_identidad_usuario(uuid, uuid);

delete from public.schema_migrations where version = '027';

commit;

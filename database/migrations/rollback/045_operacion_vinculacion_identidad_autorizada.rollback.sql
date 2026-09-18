begin;

drop function if exists public.rpc_operar_vinculacion_identidad(uuid,uuid,uuid,text,text);
drop function if exists public.rpc_solicitar_vinculacion_invitacion(text,uuid);

drop index if exists public.ux_invitaciones_acceso_auth_solicitado_abierto;

alter table public.invitaciones_acceso
  drop column if exists identidad_solicitada_at,
  drop column if exists auth_user_id_solicitado;

delete from public.schema_migrations where version = '045';

commit;

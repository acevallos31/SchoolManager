begin;

drop function if exists public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz);

drop policy if exists invitaciones_acceso_denegar_data_api on public.invitaciones_acceso;

drop index if exists public.ix_invitaciones_acceso_estado_expira;
drop index if exists public.ux_invitaciones_acceso_token_hash;

alter table public.invitaciones_acceso
  drop constraint if exists ck_invitaciones_acceso_expiracion,
  drop constraint if exists ck_invitaciones_acceso_intentos_envio,
  drop constraint if exists ck_invitaciones_acceso_token_hash_sha256,
  drop column if exists ultimo_error_envio,
  drop column if exists intentos_envio,
  drop column if exists revocado_at,
  drop column if exists rechazado_at,
  drop column if exists aprobado_at,
  drop column if exists aceptado_at,
  drop column if exists enviado_at,
  drop column if exists expira_at,
  drop column if exists token_emitido_at,
  drop column if exists token_hash;

delete from public.schema_migrations where version = '043';

commit;

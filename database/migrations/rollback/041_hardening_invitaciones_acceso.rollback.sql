begin;

-- Restaura exactamente el contrato de permisos de 040.
grant execute on function public.rpc_preparar_invitacion_usuario(uuid,text,text,text,uuid,text)
  to authenticated;

drop index if exists public.ix_invitaciones_acceso_solicitada_por;
drop index if exists public.ix_invitaciones_acceso_rol_id;
drop index if exists public.ix_invitaciones_acceso_usuario_id;
drop index if exists public.ix_invitaciones_acceso_persona_id;

delete from public.schema_migrations where version = '041';

commit;

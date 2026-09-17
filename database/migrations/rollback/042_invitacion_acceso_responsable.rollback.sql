begin;

drop function if exists public.rpc_preparar_invitacion_responsable(uuid);
delete from public.schema_migrations where version = '042';

commit;

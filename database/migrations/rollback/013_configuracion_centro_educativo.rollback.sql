begin;

drop function if exists public.rpc_actualizar_institucion(
  uuid, text, text, text, text, text, text, boolean, boolean, boolean, text[]);
drop function if exists public.rpc_crear_institucion(
  text, text, text, text, text, text, boolean, boolean, boolean, text[]);
drop function if exists public.rpc_obtener_configuracion_institucion(uuid);

alter table public.configuracion_identificadores disable row level security;
alter table public.instituciones disable row level security;

delete from public.schema_migrations where version = '013';

commit;

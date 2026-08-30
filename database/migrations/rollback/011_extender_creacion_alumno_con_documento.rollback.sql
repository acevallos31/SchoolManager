begin;

drop function if exists public.rpc_crear_alumno_nueva_persona_con_documento(
  uuid, text, text, text, text, date, text, text);
drop function if exists public.crear_alumno_nueva_persona_con_documento(
  uuid, text, text, text, text, date, text, text);
delete from public.schema_migrations where version = '011';

commit;

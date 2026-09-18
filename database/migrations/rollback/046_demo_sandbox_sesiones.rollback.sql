begin;

drop trigger if exists trg_instituciones_proteger_tipo_demo
  on public.instituciones;
drop trigger if exists trg_demo_sessions_validar_tipos
  on public.demo_sessions;

drop function if exists public.proteger_tipo_institucion_demo();
drop function if exists public.validar_demo_session_tipos();

drop table if exists public.demo_sessions;

alter table public.instituciones
  drop constraint if exists ck_instituciones_tipo;

alter table public.instituciones
  drop column if exists tipo;

delete from public.schema_migrations where version = '046';

commit;

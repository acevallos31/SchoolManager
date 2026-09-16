begin;

drop trigger if exists trg_usuarios_roles_autoridad_institucional_estricta_before
  on public.usuarios_roles;
drop function if exists public.trg_usuarios_roles_autoridad_institucional_estricta();

drop trigger if exists trg_roles_permisos_autoridad_institucional_estricta_before
  on public.roles_permisos;
drop function if exists public.trg_roles_permisos_autoridad_institucional_estricta();

drop trigger if exists trg_roles_autoridad_institucional_estricta_before
  on public.roles;
drop function if exists public.trg_roles_autoridad_institucional_estricta();

drop function if exists public.usuario_tiene_permiso_institucional_estricto(text, uuid);

delete from public.schema_migrations where version = '036';

commit;

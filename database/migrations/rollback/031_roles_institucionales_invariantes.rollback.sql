begin;

drop trigger if exists trg_roles_permisos_validar_institucional_before on public.roles_permisos;
drop function if exists public.trg_roles_permisos_validar_institucional();
drop function if exists public.contar_admins_institucionales(uuid);
delete from public.schema_migrations where version='031';

commit;

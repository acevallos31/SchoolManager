select '037 no registrado' as error
where not exists (
  select 1 from public.schema_migrations where version = '037'
);

select 'authenticated no puede ejecutar helper institucional estricto' as error
where not has_function_privilege(
  'authenticated',
  'public.usuario_tiene_permiso_institucional_estricto(text,uuid)',
  'EXECUTE'
);

select 'roles_select no usa autoridad institucional estricta' as error
where not exists (
  select 1
  from pg_policies
  where schemaname = 'public'
    and tablename = 'roles'
    and policyname = 'roles_select'
    and qual like '%usuario_tiene_permiso_institucional_estricto%'
);

select 'roles_permisos_select no usa autoridad institucional estricta' as error
where not exists (
  select 1
  from pg_policies
  where schemaname = 'public'
    and tablename = 'roles_permisos'
    and policyname = 'roles_permisos_select'
    and qual like '%usuario_tiene_permiso_institucional_estricto%'
);

select 'usuarios_roles_select no usa autoridad institucional estricta' as error
where not exists (
  select 1
  from pg_policies
  where schemaname = 'public'
    and tablename = 'usuarios_roles'
    and policyname = 'usuarios_roles_select'
    and qual like '%usuario_tiene_permiso_institucional_estricto%'
);

select '042 no registrado' as error
where not exists (select 1 from public.schema_migrations where version = '042');

select 'falta rpc_preparar_invitacion_responsable(uuid)' as error
where to_regprocedure('public.rpc_preparar_invitacion_responsable(uuid)') is null;

select 'authenticated puede ejecutar rpc_preparar_invitacion_responsable' as error
where has_function_privilege(
  'authenticated', 'public.rpc_preparar_invitacion_responsable(uuid)', 'EXECUTE'
);

select 'anon puede ejecutar rpc_preparar_invitacion_responsable' as error
where has_function_privilege(
  'anon', 'public.rpc_preparar_invitacion_responsable(uuid)', 'EXECUTE'
);

select 'service_role perdio acceso a rpc_preparar_invitacion_responsable' as error
where not has_function_privilege(
  'service_role', 'public.rpc_preparar_invitacion_responsable(uuid)', 'EXECUTE'
);

select 'rpc_preparar_invitacion_responsable no es SECURITY DEFINER' as error
where not exists (
  select 1
  from pg_proc p
  join pg_namespace n on n.oid = p.pronamespace
  where n.nspname = 'public'
    and p.proname = 'rpc_preparar_invitacion_responsable'
    and pg_get_function_identity_arguments(p.oid) = 'p_responsable_id uuid'
    and p.prosecdef
);

select 'rpc_preparar_invitacion_responsable no fija search_path' as error
where not exists (
  select 1
  from pg_proc p
  join pg_namespace n on n.oid = p.pronamespace
  where n.nspname = 'public'
    and p.proname = 'rpc_preparar_invitacion_responsable'
    and coalesce(array_to_string(p.proconfig, ','), '') like '%search_path=pg_catalog, public, pg_temp%'
);

select '045 no registrado' as error
where not exists (select 1 from public.schema_migrations where version = '045');

select 'falta auth_user_id_solicitado' as error
where not exists (
  select 1 from information_schema.columns
  where table_schema='public'
    and table_name='invitaciones_acceso'
    and column_name='auth_user_id_solicitado'
);

select 'falta identidad_solicitada_at' as error
where not exists (
  select 1 from information_schema.columns
  where table_schema='public'
    and table_name='invitaciones_acceso'
    and column_name='identidad_solicitada_at'
);

select 'rpc_solicitar_vinculacion_invitacion no existe' as error
where to_regprocedure('public.rpc_solicitar_vinculacion_invitacion(text,uuid)') is null;

select 'rpc_operar_vinculacion_identidad no existe' as error
where to_regprocedure('public.rpc_operar_vinculacion_identidad(uuid,uuid,text,text)') is null;

select 'rpc_solicitar debe ser SECURITY DEFINER' as error
where exists (
  select 1
  from pg_proc p
  join pg_namespace n on n.oid=p.pronamespace
  where n.nspname='public'
    and p.proname='rpc_solicitar_vinculacion_invitacion'
    and not p.prosecdef
);

select 'rpc_operar debe ser SECURITY DEFINER' as error
where exists (
  select 1
  from pg_proc p
  join pg_namespace n on n.oid=p.pronamespace
  where n.nspname='public'
    and p.proname='rpc_operar_vinculacion_identidad'
    and not p.prosecdef
);

select 'authenticated puede ejecutar rpc_solicitar' as error
where has_function_privilege(
  'authenticated',
  'public.rpc_solicitar_vinculacion_invitacion(text,uuid)',
  'EXECUTE'
);

select 'authenticated puede ejecutar rpc_operar' as error
where has_function_privilege(
  'authenticated',
  'public.rpc_operar_vinculacion_identidad(uuid,uuid,text,text)',
  'EXECUTE'
);

select 'anon puede ejecutar rpc_solicitar' as error
where has_function_privilege(
  'anon',
  'public.rpc_solicitar_vinculacion_invitacion(text,uuid)',
  'EXECUTE'
);

select 'anon puede ejecutar rpc_operar' as error
where has_function_privilege(
  'anon',
  'public.rpc_operar_vinculacion_identidad(uuid,uuid,text,text)',
  'EXECUTE'
);

select 'service_role no puede ejecutar rpc_solicitar' as error
where not has_function_privilege(
  'service_role',
  'public.rpc_solicitar_vinculacion_invitacion(text,uuid)',
  'EXECUTE'
);

select 'service_role no puede ejecutar rpc_operar' as error
where not has_function_privilege(
  'service_role',
  'public.rpc_operar_vinculacion_identidad(uuid,uuid,text,text)',
  'EXECUTE'
);

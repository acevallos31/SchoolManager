select '043 no registrado' as error
where not exists (select 1 from public.schema_migrations where version = '043');

select 'falta columna token_hash' as error
where not exists (
  select 1 from information_schema.columns
  where table_schema='public' and table_name='invitaciones_acceso' and column_name='token_hash'
);

select 'falta columna expira_at' as error
where not exists (
  select 1 from information_schema.columns
  where table_schema='public' and table_name='invitaciones_acceso' and column_name='expira_at'
);

select 'falta columna intentos_envio' as error
where not exists (
  select 1 from information_schema.columns
  where table_schema='public' and table_name='invitaciones_acceso' and column_name='intentos_envio'
);

select 'falta indice unico de token hash' as error
where to_regclass('public.ux_invitaciones_acceso_token_hash') is null;

select 'falta indice estado/expiracion' as error
where to_regclass('public.ix_invitaciones_acceso_estado_expira') is null;

select 'RLS no esta habilitado en invitaciones_acceso' as error
where not exists (
  select 1 from pg_class c join pg_namespace n on n.oid=c.relnamespace
  where n.nspname='public' and c.relname='invitaciones_acceso' and c.relrowsecurity
);

select 'falta policy deny-by-default de Data API' as error
where not exists (
  select 1 from pg_policies
  where schemaname='public'
    and tablename='invitaciones_acceso'
    and policyname='invitaciones_acceso_denegar_data_api'
    and permissive='RESTRICTIVE'
    and cmd='ALL'
);

select 'authenticated conserva SELECT directo sobre invitaciones_acceso' as error
where has_table_privilege('authenticated','public.invitaciones_acceso','SELECT');

select 'anon conserva SELECT directo sobre invitaciones_acceso' as error
where has_table_privilege('anon','public.invitaciones_acceso','SELECT');

select 'service_role perdio SELECT sobre invitaciones_acceso' as error
where not has_table_privilege('service_role','public.invitaciones_acceso','SELECT');

select 'falta rpc_emitir_invitacion_acceso' as error
where to_regprocedure('public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz)') is null;

select 'authenticated puede ejecutar rpc_emitir_invitacion_acceso' as error
where has_function_privilege(
  'authenticated','public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz)','EXECUTE'
);

select 'anon puede ejecutar rpc_emitir_invitacion_acceso' as error
where has_function_privilege(
  'anon','public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz)','EXECUTE'
);

select 'service_role perdio acceso a rpc_emitir_invitacion_acceso' as error
where not has_function_privilege(
  'service_role','public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz)','EXECUTE'
);

select 'rpc_emitir_invitacion_acceso no es SECURITY DEFINER' as error
where not exists (
  select 1 from pg_proc p join pg_namespace n on n.oid=p.pronamespace
  where n.nspname='public'
    and p.proname='rpc_emitir_invitacion_acceso'
    and pg_get_function_identity_arguments(p.oid)='p_invitacion_id uuid, p_token_hash text, p_expira_at timestamp with time zone'
    and p.prosecdef
);

select 'rpc_emitir_invitacion_acceso no fija search_path' as error
where not exists (
  select 1 from pg_proc p join pg_namespace n on n.oid=p.pronamespace
  where n.nspname='public'
    and p.proname='rpc_emitir_invitacion_acceso'
    and coalesce(array_to_string(p.proconfig,','),'') like '%search_path=pg_catalog, public, pg_temp%'
);

select '044 no registrado' as error
where not exists (select 1 from public.schema_migrations where version = '044');

select 'falta emision_version' as error
where not exists (
  select 1 from information_schema.columns
  where table_schema='public' and table_name='invitaciones_acceso' and column_name='emision_version'
);

select 'falta proveedor_envio' as error
where not exists (
  select 1 from information_schema.columns
  where table_schema='public' and table_name='invitaciones_acceso' and column_name='proveedor_envio'
);

select 'falta proveedor_mensaje_id' as error
where not exists (
  select 1 from information_schema.columns
  where table_schema='public' and table_name='invitaciones_acceso' and column_name='proveedor_mensaje_id'
);

select 'falta rpc_confirmar_envio_invitacion' as error
where to_regprocedure('public.rpc_confirmar_envio_invitacion(uuid,bigint,text,text)') is null;

select 'falta rpc_registrar_error_envio_invitacion' as error
where to_regprocedure('public.rpc_registrar_error_envio_invitacion(uuid,bigint,text)') is null;

select 'authenticated puede ejecutar rpc_emitir_invitacion_acceso' as error
where has_function_privilege('authenticated','public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz)','EXECUTE');

select 'authenticated puede ejecutar rpc_confirmar_envio_invitacion' as error
where has_function_privilege('authenticated','public.rpc_confirmar_envio_invitacion(uuid,bigint,text,text)','EXECUTE');

select 'authenticated puede ejecutar rpc_registrar_error_envio_invitacion' as error
where has_function_privilege('authenticated','public.rpc_registrar_error_envio_invitacion(uuid,bigint,text)','EXECUTE');

select 'service_role perdio rpc_emitir_invitacion_acceso' as error
where not has_function_privilege('service_role','public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz)','EXECUTE');

select 'service_role perdio rpc_confirmar_envio_invitacion' as error
where not has_function_privilege('service_role','public.rpc_confirmar_envio_invitacion(uuid,bigint,text,text)','EXECUTE');

select 'service_role perdio rpc_registrar_error_envio_invitacion' as error
where not has_function_privilege('service_role','public.rpc_registrar_error_envio_invitacion(uuid,bigint,text)','EXECUTE');

select 'RLS no esta habilitado en invitaciones_acceso' as error
where not exists (
  select 1 from pg_class c join pg_namespace n on n.oid=c.relnamespace
  where n.nspname='public' and c.relname='invitaciones_acceso' and c.relrowsecurity
);

select 'falta policy restrictiva invitaciones_acceso_denegar_data_api' as error
where not exists (
  select 1 from pg_policies
  where schemaname='public' and tablename='invitaciones_acceso'
    and policyname='invitaciones_acceso_denegar_data_api'
    and permissive='RESTRICTIVE'
);

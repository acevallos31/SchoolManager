select '041 no registrado' as error
where not exists (select 1 from public.schema_migrations where version = '041');

select 'authenticated aun puede ejecutar rpc_preparar_invitacion_usuario' as error
where has_function_privilege(
  'authenticated',
  'public.rpc_preparar_invitacion_usuario(uuid,text,text,text,uuid,text)',
  'EXECUTE'
);

select 'service_role perdio acceso a rpc_preparar_invitacion_usuario' as error
where not has_function_privilege(
  'service_role',
  'public.rpc_preparar_invitacion_usuario(uuid,text,text,text,uuid,text)',
  'EXECUTE'
);

select 'falta indice invitaciones_acceso.persona_id' as error
where not exists (
  select 1 from pg_indexes
  where schemaname = 'public'
    and tablename = 'invitaciones_acceso'
    and indexname = 'ix_invitaciones_acceso_persona_id'
);

select 'falta indice invitaciones_acceso.usuario_id' as error
where not exists (
  select 1 from pg_indexes
  where schemaname = 'public'
    and tablename = 'invitaciones_acceso'
    and indexname = 'ix_invitaciones_acceso_usuario_id'
);

select 'falta indice invitaciones_acceso.rol_id' as error
where not exists (
  select 1 from pg_indexes
  where schemaname = 'public'
    and tablename = 'invitaciones_acceso'
    and indexname = 'ix_invitaciones_acceso_rol_id'
);

select 'falta indice invitaciones_acceso.solicitada_por' as error
where not exists (
  select 1 from pg_indexes
  where schemaname = 'public'
    and tablename = 'invitaciones_acceso'
    and indexname = 'ix_invitaciones_acceso_solicitada_por'
);

select 'RLS de invitaciones_acceso quedo deshabilitado' as error
where not exists (
  select 1
  from pg_class c
  join pg_namespace n on n.oid = c.relnamespace
  where n.nspname = 'public'
    and c.relname = 'invitaciones_acceso'
    and c.relrowsecurity
);

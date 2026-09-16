select '040 no registrado' as error
where not exists (select 1 from public.schema_migrations where version = '040');

select 'tabla invitaciones_acceso no existe' as error
where to_regclass('public.invitaciones_acceso') is null;

select 'rpc_preparar_invitacion_usuario no existe' as error
where to_regprocedure('public.rpc_preparar_invitacion_usuario(uuid,text,text,text,uuid,text)') is null;

select 'usuarios.auth_user_id sigue NOT NULL' as error
where exists (
  select 1
  from information_schema.columns
  where table_schema = 'public'
    and table_name = 'usuarios'
    and column_name = 'auth_user_id'
    and is_nullable = 'NO'
);

select 'falta unicidad de invitacion abierta por usuario/institucion' as error
where not exists (
  select 1
  from pg_indexes
  where schemaname = 'public'
    and tablename = 'invitaciones_acceso'
    and indexname = 'ux_invitaciones_acceso_abierta_usuario'
);

select 'authenticated tiene acceso directo a invitaciones_acceso' as error
where has_table_privilege('authenticated', 'public.invitaciones_acceso', 'select')
   or has_table_privilege('authenticated', 'public.invitaciones_acceso', 'insert')
   or has_table_privilege('authenticated', 'public.invitaciones_acceso', 'update')
   or has_table_privilege('authenticated', 'public.invitaciones_acceso', 'delete');

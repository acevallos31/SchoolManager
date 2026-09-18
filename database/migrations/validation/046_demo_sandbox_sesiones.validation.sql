select '046 no registrado' as error
where not exists (
  select 1 from public.schema_migrations where version = '046'
);

select 'falta instituciones.tipo' as error
where not exists (
  select 1
  from information_schema.columns
  where table_schema = 'public'
    and table_name = 'instituciones'
    and column_name = 'tipo'
    and is_nullable = 'NO'
);

select 'instituciones existentes no son normal por defecto' as error
where exists (
  select 1 from public.instituciones where tipo is distinct from 'normal'
)
and not exists (
  select 1 from public.instituciones where tipo in ('demo_template', 'demo_sandbox')
);

select 'falta tabla demo_sessions' as error
where to_regclass('public.demo_sessions') is null;

select 'demo_sessions sin RLS' as error
where exists (
  select 1
  from pg_class c
  join pg_namespace n on n.oid = c.relnamespace
  where n.nspname = 'public'
    and c.relname = 'demo_sessions'
    and not c.relrowsecurity
);

select 'anon tiene privilegios sobre demo_sessions' as error
where has_table_privilege('anon', 'public.demo_sessions', 'select')
   or has_table_privilege('anon', 'public.demo_sessions', 'insert')
   or has_table_privilege('anon', 'public.demo_sessions', 'update')
   or has_table_privilege('anon', 'public.demo_sessions', 'delete');

select 'authenticated tiene privilegios sobre demo_sessions' as error
where has_table_privilege('authenticated', 'public.demo_sessions', 'select')
   or has_table_privilege('authenticated', 'public.demo_sessions', 'insert')
   or has_table_privilege('authenticated', 'public.demo_sessions', 'update')
   or has_table_privilege('authenticated', 'public.demo_sessions', 'delete');

select 'falta indice de sesion activa por identidad' as error
where to_regclass('public.ux_demo_sessions_auth_activa') is null;

select 'falta indice unico de sandbox por sesion' as error
where to_regclass('public.ux_demo_sessions_institucion') is null;

select 'falta trigger de tipos demo_sessions' as error
where not exists (
  select 1
  from pg_trigger
  where tgrelid = 'public.demo_sessions'::regclass
    and tgname = 'trg_demo_sessions_validar_tipos'
    and not tgisinternal
);

select 'falta trigger protector de instituciones Demo' as error
where not exists (
  select 1
  from pg_trigger
  where tgrelid = 'public.instituciones'::regclass
    and tgname = 'trg_instituciones_proteger_tipo_demo'
    and not tgisinternal
);

select 'funcion validar_demo_session_tipos ejecutable por authenticated' as error
where has_function_privilege(
  'authenticated',
  'public.validar_demo_session_tipos()',
  'EXECUTE'
);

select 'funcion proteger_tipo_institucion_demo ejecutable por authenticated' as error
where has_function_privilege(
  'authenticated',
  'public.proteger_tipo_institucion_demo()',
  'EXECUTE'
);

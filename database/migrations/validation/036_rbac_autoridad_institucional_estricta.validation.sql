select '036 no registrado' as error
where not exists (
  select 1 from public.schema_migrations where version = '036'
);

select 'helper de autoridad institucional estricta no existe' as error
where to_regprocedure(
  'public.usuario_tiene_permiso_institucional_estricto(text,uuid)'
) is null;

select 'trigger de autoridad sobre roles no existe' as error
where not exists (
  select 1
  from pg_trigger t
  join pg_class c on c.oid = t.tgrelid
  join pg_namespace n on n.oid = c.relnamespace
  where n.nspname = 'public'
    and c.relname = 'roles'
    and t.tgname = 'trg_roles_autoridad_institucional_estricta_before'
    and not t.tgisinternal
);

select 'trigger de autoridad sobre roles_permisos no existe' as error
where not exists (
  select 1
  from pg_trigger t
  join pg_class c on c.oid = t.tgrelid
  join pg_namespace n on n.oid = c.relnamespace
  where n.nspname = 'public'
    and c.relname = 'roles_permisos'
    and t.tgname = 'trg_roles_permisos_autoridad_institucional_estricta_before'
    and not t.tgisinternal
);

select 'trigger de autoridad sobre usuarios_roles no existe' as error
where not exists (
  select 1
  from pg_trigger t
  join pg_class c on c.oid = t.tgrelid
  join pg_namespace n on n.oid = c.relnamespace
  where n.nspname = 'public'
    and c.relname = 'usuarios_roles'
    and t.tgname = 'trg_usuarios_roles_autoridad_institucional_estricta_before'
    and not t.tgisinternal
);

select 'helper estricto no exige institucion exacta' as error
where position(
  'ur.institucion_id = p_institucion_id'
  in pg_get_functiondef(
    'public.usuario_tiene_permiso_institucional_estricto(text,uuid)'::regprocedure
  )
) = 0;

select 'helper estricto no limita la excepcion global a platform_admin' as error
where position(
  'r.codigo = ''platform_admin'''
  in pg_get_functiondef(
    'public.usuario_tiene_permiso_institucional_estricto(text,uuid)'::regprocedure
  )
) = 0;

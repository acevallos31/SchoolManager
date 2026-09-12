-- Validacion 027 - cada fila devuelta es un hallazgo.
-- Solo lectura: no modifica datos ni vincula identidades.

-- 1. Migracion registrada exactamente una vez.
select '027_no_registrada' as error
where not exists (
  select 1 from public.schema_migrations where version = '027'
);

select '027_duplicada' as error
from public.schema_migrations
where version = '027'
group by version
having count(*) > 1;

-- 2. La RPC de vinculacion existe con la firma esperada.
select '027_funcion_faltante' as error
where to_regprocedure('public.vincular_identidad_usuario(uuid, uuid)') is null;

-- 3. Es SECURITY DEFINER y fija search_path VACIO (no hereda el del invocador
--    ni resuelve nada por un esquema implicito).
select '027_no_security_definer' as error
where exists (
  select 1
  from pg_proc p
  join pg_namespace n on n.oid = p.pronamespace
  where n.nspname = 'public'
    and p.proname = 'vincular_identidad_usuario'
    and not p.prosecdef
);

select '027_search_path_faltante' as error
where exists (
  select 1
  from pg_proc p
  join pg_namespace n on n.oid = p.pronamespace
  where n.nspname = 'public'
    and p.proname = 'vincular_identidad_usuario'
    and not exists (
      select 1
      from unnest(coalesce(p.proconfig, '{}'::text[])) as c
      where c = 'search_path=""'
    )
);

-- 4. Sin EXECUTE para public/anon/authenticated (solo service_role y owner).
select '027_execute_expuesto_a_public' as error
where has_function_privilege(
  'public',
  'public.vincular_identidad_usuario(uuid, uuid)',
  'execute'
);

select '027_execute_expuesto_a_anon' as error
where exists (select 1 from pg_roles where rolname = 'anon')
  and has_function_privilege(
    'anon',
    'public.vincular_identidad_usuario(uuid, uuid)',
    'execute'
  );

select '027_execute_expuesto_a_authenticated' as error
where exists (select 1 from pg_roles where rolname = 'authenticated')
  and has_function_privilege(
    'authenticated',
    'public.vincular_identidad_usuario(uuid, uuid)',
    'execute'
  );

select '027_service_role_sin_execute' as error
where exists (select 1 from pg_roles where rolname = 'service_role')
  and not has_function_privilege(
    'service_role',
    'public.vincular_identidad_usuario(uuid, uuid)',
    'execute'
  );

-- 5. La defensa de unicidad de la identidad sigue presente en la tabla.
select '027_indice_unico_auth_user_id_faltante' as error
where not exists (
  select 1
  from pg_index i
  join pg_class c on c.oid = i.indexrelid
  where c.relname = 'ux_usuarios_auth_user_id'
    and i.indisunique
);

-- 6. Ninguna identidad quedo compartida por dos usuarios.
select '027_auth_user_id_duplicado' as error
from public.usuarios
where auth_user_id is not null
group by auth_user_id
having count(*) > 1;

-- 7. La funcion no debe repartir roles ni permisos: solo actualiza la columna
-- de identidad de public.usuarios.
select '027_funcion_escribe_otras_tablas' as error
where exists (
  select 1
  from pg_proc p
  join pg_namespace n on n.oid = p.pronamespace
  where n.nspname = 'public'
    and p.proname = 'vincular_identidad_usuario'
    and (
      pg_get_functiondef(p.oid) ~* 'insert\s+into\s+public\.usuarios_roles'
      or pg_get_functiondef(p.oid) ~* 'update\s+public\.roles'
      or pg_get_functiondef(p.oid) ~* 'grant\s+'
    )
);

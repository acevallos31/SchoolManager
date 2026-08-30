-- Validacion 010. Las consultas de diagnostico deben devolver cero filas.
select 'usuarios.rol todavia existe' as error
where exists (
  select 1 from information_schema.columns
  where table_schema = 'public' and table_name = 'usuarios' and column_name = 'rol'
);

select u.id as usuario_activo_sin_rol_rbac
from public.usuarios u
where u.activo
  and not exists (
    select 1
    from public.usuarios_roles ur
    join public.roles r on r.id = ur.rol_id
    where ur.usuario_id = u.id and ur.activo and r.activo
  );

select 'auth_user_id no es unico y obligatorio' as error
where not exists (
  select 1 from pg_indexes
  where schemaname = 'public' and tablename = 'usuarios'
    and indexname = 'ux_usuarios_auth_user_id'
)
or exists (select 1 from public.usuarios where auth_user_id is null);

select d.classid::regclass, d.objid::regclass
from pg_depend d
join pg_class c on c.oid = d.refobjid
join pg_attribute a on a.attrelid = c.oid and a.attnum = d.refobjsubid
where c.oid = 'public.usuarios'::regclass and a.attname = 'rol';

select 'RLS dejo de estar habilitada' as error
where exists (
  select 1
  from (values ('usuarios'), ('usuarios_roles'), ('roles'), ('permisos')) esperado(nombre)
  join pg_class c on c.relname = esperado.nombre and c.relnamespace = 'public'::regnamespace
  where not c.relrowsecurity
);

select 'usuario_tiene_permiso no esta disponible' as error
where to_regprocedure('public.usuario_tiene_permiso(uuid,text,uuid)') is null;

select version, nombre
from public.schema_migrations
where version = '010';

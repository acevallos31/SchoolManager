-- Fase 1C - Bloque 4. Contraccion final del campo legacy usuarios.rol.
-- Debe aplicarse solo despues de desplegar un backend compatible con RBAC multirol.

begin;

do $$
begin
  if to_regclass('public.roles') is null
     or to_regclass('public.usuarios_roles') is null
     or to_regclass('public.roles_permisos') is null then
    raise exception 'Migracion 010 requiere el modelo RBAC de la migracion 007.';
  end if;

  if not exists (
    select 1
    from information_schema.columns
    where table_schema = 'public' and table_name = 'usuarios' and column_name = 'rol'
  ) then
    raise exception 'Migracion 010 esperaba la columna public.usuarios.rol.';
  end if;

  if exists (
    select 1
    from public.usuarios u
    where not exists (
      select 1
      from public.usuarios_roles ur
      join public.roles r on r.id = ur.rol_id
      where ur.usuario_id = u.id
        and ur.activo = true
        and r.activo = true
        and r.codigo = u.rol
    )
  ) then
    raise exception 'Migracion 010 detecto roles legacy sin una asignacion RBAC activa equivalente.';
  end if;

  if exists (
    select 1
    from pg_depend d
    join pg_class c on c.oid = d.refobjid
    join pg_attribute a on a.attrelid = c.oid and a.attnum = d.refobjsubid
    where c.oid = 'public.usuarios'::regclass
      and a.attname = 'rol'
      and d.deptype not in ('a', 'i')
      and d.classid <> 'pg_constraint'::regclass
  ) then
    raise exception 'Migracion 010 detecto objetos SQL no previstos dependientes de usuarios.rol.';
  end if;
end;
$$;

alter table public.usuarios drop constraint if exists ck_usuarios_rol;
alter table public.usuarios drop column rol;

insert into public.schema_migrations (version, nombre, checksum)
values ('010', 'retirar_rol_legacy_y_consolidar_identidad', null)
on conflict (version) do nothing;

commit;

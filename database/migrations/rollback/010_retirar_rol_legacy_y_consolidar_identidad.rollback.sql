-- Rollback 010: solo es reversible si cada usuario tiene exactamente una
-- asignacion activa, global y compatible con los cuatro roles legacy.

begin;

do $$
begin
  if exists (
    select 1
    from information_schema.columns
    where table_schema = 'public' and table_name = 'usuarios' and column_name = 'rol'
  ) then
    raise exception 'Rollback 010 esperaba que public.usuarios.rol no existiera.';
  end if;

  if exists (
    select u.id
    from public.usuarios u
    left join public.usuarios_roles ur
      on ur.usuario_id = u.id and ur.activo = true
    left join public.roles r
      on r.id = ur.rol_id and r.activo = true
       and ur.institucion_id is null
       and r.codigo in ('admin', 'operador', 'usuario', 'padre')
    group by u.id
    having count(r.id) <> 1
       or count(ur.id) <> 1
  ) then
    raise exception 'Rollback 010 ambiguo: cada usuario requiere exactamente un rol global legacy compatible.';
  end if;
end;
$$;

alter table public.usuarios add column rol text;

update public.usuarios u
set rol = r.codigo
from public.usuarios_roles ur
join public.roles r on r.id = ur.rol_id
where ur.usuario_id = u.id
  and ur.activo = true
  and ur.institucion_id is null
  and r.activo = true
  and r.codigo in ('admin', 'operador', 'usuario', 'padre');

alter table public.usuarios alter column rol set not null;
alter table public.usuarios add constraint ck_usuarios_rol
  check (rol in ('admin', 'operador', 'usuario', 'padre'));

delete from public.schema_migrations where version = '010';

commit;

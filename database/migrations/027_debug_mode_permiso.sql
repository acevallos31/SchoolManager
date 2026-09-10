-- Bloque 034 - permiso de aplicación para Debug Mode controlado.
begin;

do $$ begin
  if not exists (select 1 from public.schema_migrations where version = '026') then
    raise exception 'Migracion 027 requiere la migracion 026.';
  end if;
end $$;

insert into public.permisos (codigo, modulo, nombre) values
  ('sistema.debug.ver', 'sistema', 'Ver y activar diagnóstico técnico temporal')
on conflict (codigo) do nothing;

insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'admin'
  and p.codigo = 'sistema.debug.ver'
on conflict do nothing;

insert into public.schema_migrations (version, nombre, checksum)
values ('027', 'debug_mode_permiso', null)
on conflict (version) do nothing;

commit;

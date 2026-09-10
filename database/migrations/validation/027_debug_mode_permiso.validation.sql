-- Validacion 027 - cada fila devuelta es un hallazgo.

select '027_no_registrada' as error
where not exists (
  select 1 from public.schema_migrations where version = '027'
);

select 'permiso_debug_faltante' as error
where not exists (
  select 1 from public.permisos where codigo = 'sistema.debug.ver'
);

select 'permiso_debug_sin_admin' as error
where not exists (
  select 1
  from public.roles_permisos rp
  join public.roles r on r.id = rp.rol_id
  join public.permisos p on p.id = rp.permiso_id
  where r.codigo = 'admin'
    and p.codigo = 'sistema.debug.ver'
);

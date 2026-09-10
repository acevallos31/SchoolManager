begin;

delete from public.roles_permisos rp
using public.permisos p
where rp.permiso_id = p.id
  and p.codigo = 'sistema.debug.ver';

delete from public.permisos where codigo = 'sistema.debug.ver';
delete from public.schema_migrations where version = '027';

commit;

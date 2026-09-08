-- Validacion 024: un solo inventario de permisos requeridos evita repetir
-- la comprobacion de catalogo para aplicacion y capa interna. Cada fila es
-- un hallazgo; conserva controles de admin activo y registro de migracion.
with requeridos as (
  select 'academico.estructura.' || accion as codigo,
         'permiso_aplicacion_faltante' as diagnostico
  from unnest(array['ver', 'editar', 'desactivar']) as acciones(accion)
  union all
  select 'configuracion.' || entidad || '.' || accion,
         'permiso_interno_faltante'
  from unnest(array['grados', 'jornadas', 'secciones']) as entidades(entidad)
  cross join unnest(array['ver', 'crear', 'editar', 'desactivar']) as acciones(accion)
), faltantes as (
  select r.diagnostico, r.codigo
  from requeridos r
  left join public.permisos p using (codigo)
  where p.id is null
), grants_admin as (
  select rp.permiso_id
  from public.roles_permisos rp
  join public.roles r on r.id = rp.rol_id
  where r.codigo = 'admin' and r.activo
)
select diagnostico, codigo from faltantes
union all
select 'permiso_sin_grant_admin', p.codigo
from public.permisos p
left join grants_admin g on g.permiso_id = p.id
where p.codigo like 'academico.estructura.%' and g.permiso_id is null
union all
select 'migracion_no_registrada', '024'
where not exists (select 1 from public.schema_migrations where version = '024');

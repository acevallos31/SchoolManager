-- Validacion Migracion 018: configuracion financiera (conceptos + planes de pago).
-- Debe ejecutarse sin error y devolver resultados no vacios si la migracion es correcta.

-- 1. Privilegios de permisos insertados.
select p.codigo
from public.permisos p
where p.codigo in (
  'configuracion.conceptos_financieros.ver',
  'configuracion.conceptos_financieros.crear',
  'configuracion.conceptos_financieros.editar',
  'configuracion.conceptos_financieros.desactivar',
  'configuracion.planes_pago.ver',
  'configuracion.planes_pago.crear',
  'configuracion.planes_pago.editar',
  'configuracion.planes_pago.desactivar'
);

-- 2. El rol admin tiene los permisos asignados.
select count(*) as admin_permisos
from public.roles_permisos rp
join public.roles r on r.id = rp.rol_id and r.codigo = 'admin'
join public.permisos p on p.id = rp.permiso_id
where p.codigo in (
  'configuracion.conceptos_financieros.ver',
  'configuracion.conceptos_financieros.crear',
  'configuracion.conceptos_financieros.editar',
  'configuracion.conceptos_financieros.desactivar',
  'configuracion.planes_pago.ver',
  'configuracion.planes_pago.crear',
  'configuracion.planes_pago.editar',
  'configuracion.planes_pago.desactivar'
);

-- 3. Funciones de concepto existentes.
select proname
from pg_proc p join pg_namespace n on n.oid = p.pronamespace
where n.nspname = 'public'
  and proname in (
    'rpc_listar_conceptos_financieros',
    'rpc_crear_concepto_financiero',
    'rpc_actualizar_concepto_financiero',
    'rpc_desactivar_concepto_financiero',
    'rpc_reactivar_concepto_financiero'
  );

-- 4. Funciones de planes existentes.
select proname
from pg_proc p join pg_namespace n on n.oid = p.pronamespace
where n.nspname = 'public'
  and proname in (
    'rpc_listar_planes_pago',
    'rpc_obtener_plan_pago',
    'rpc_crear_plan_pago',
    'rpc_actualizar_plan_pago',
    'rpc_desactivar_plan_pago',
    'rpc_reactivar_plan_pago'
  );

-- 5. Tablas creadas.
select table_name
from information_schema.tables
where table_schema = 'public'
  and table_name in ('conceptos_financieros', 'planes_pago', 'plan_cuotas');

-- 6. Registro de la migracion.
select version, nombre
from public.schema_migrations
where version = '018';
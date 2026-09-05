-- Validacion Migracion 018: configuracion financiera (conceptos + planes de pago).
-- Los diagnosticos con sufijo *_faltante o *_indebido deben devolver cero filas.

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

-- 7. RLS debe estar habilitado en las tres tablas RPC-only.
select c.relname as rls_faltante
from pg_class c
join pg_namespace n on n.oid = c.relnamespace
where n.nspname = 'public'
  and c.relname in ('conceptos_financieros', 'planes_pago', 'plan_cuotas')
  and not c.relrowsecurity;

-- 8. anon/authenticated no deben tener privilegios directos de tabla.
select esperado_rol.rol, esperado_tabla.tabla,
       esperado_privilegio.privilegio as privilegio_indebido
from (values ('anon'), ('authenticated')) esperado_rol(rol)
cross join (values
  ('conceptos_financieros'), ('planes_pago'), ('plan_cuotas')) esperado_tabla(tabla)
cross join (values
  ('SELECT'), ('INSERT'), ('UPDATE'), ('DELETE'), ('TRUNCATE'), ('REFERENCES'), ('TRIGGER'))
  esperado_privilegio(privilegio)
where has_table_privilege(
  esperado_rol.rol,
  format('public.%I', esperado_tabla.tabla),
  esperado_privilegio.privilegio);

-- 9. Las firmas RPC exactas deben existir.
select esperado.firma as rpc_faltante
from (values
  ('rpc_listar_conceptos_financieros(uuid,boolean)'),
  ('rpc_crear_concepto_financiero(text,numeric,text,uuid)'),
  ('rpc_actualizar_concepto_financiero(uuid,text,numeric,text,uuid)'),
  ('rpc_desactivar_concepto_financiero(uuid,text,uuid)'),
  ('rpc_reactivar_concepto_financiero(uuid,uuid)'),
  ('rpc_listar_planes_pago(uuid,boolean)'),
  ('rpc_obtener_plan_pago(uuid,uuid)'),
  ('rpc_crear_plan_pago(text,text,jsonb,uuid)'),
  ('rpc_actualizar_plan_pago(uuid,text,text,jsonb,uuid)'),
  ('rpc_desactivar_plan_pago(uuid,text,uuid)'),
  ('rpc_reactivar_plan_pago(uuid,uuid)')) esperado(firma)
where to_regprocedure('public.' || esperado.firma) is null;

-- 10. Los ocho permisos deben estar asignados al rol admin.
select esperado.codigo as permiso_admin_faltante
from (values
  ('configuracion.conceptos_financieros.ver'),
  ('configuracion.conceptos_financieros.crear'),
  ('configuracion.conceptos_financieros.editar'),
  ('configuracion.conceptos_financieros.desactivar'),
  ('configuracion.planes_pago.ver'),
  ('configuracion.planes_pago.crear'),
  ('configuracion.planes_pago.editar'),
  ('configuracion.planes_pago.desactivar')) esperado(codigo)
where not exists (
  select 1
  from public.roles_permisos rp
  join public.roles r on r.id = rp.rol_id
  join public.permisos p on p.id = rp.permiso_id
  where r.codigo = 'admin' and p.codigo = esperado.codigo
);

-- 11. La migracion debe estar registrada exactamente una vez.
select '018' as migracion_faltante
where not exists (
  select 1 from public.schema_migrations
  where version = '018' and nombre = 'configuracion_financiera'
);

-- 12. La funcion auxiliar del trigger no debe ser invocable por clientes.
select esperado.rol as trigger_auxiliar_expuesto
from (values ('anon'), ('authenticated')) esperado(rol)
where has_function_privilege(
  esperado.rol,
  'public.trg_plan_cuotas_concepto_institucion()',
  'EXECUTE');

-- 13. Todas las RPC deben ser SECURITY DEFINER y fijar el search_path.
select esperado.firma as rpc_configuracion_insegura
from (values
  ('rpc_listar_conceptos_financieros(uuid,boolean)'),
  ('rpc_crear_concepto_financiero(text,numeric,text,uuid)'),
  ('rpc_actualizar_concepto_financiero(uuid,text,numeric,text,uuid)'),
  ('rpc_desactivar_concepto_financiero(uuid,text,uuid)'),
  ('rpc_reactivar_concepto_financiero(uuid,uuid)'),
  ('rpc_listar_planes_pago(uuid,boolean)'),
  ('rpc_obtener_plan_pago(uuid,uuid)'),
  ('rpc_crear_plan_pago(text,text,jsonb,uuid)'),
  ('rpc_actualizar_plan_pago(uuid,text,text,jsonb,uuid)'),
  ('rpc_desactivar_plan_pago(uuid,text,uuid)'),
  ('rpc_reactivar_plan_pago(uuid,uuid)')) esperado(firma)
join pg_proc p on p.oid = to_regprocedure('public.' || esperado.firma)
where not p.prosecdef
   or not coalesce(p.proconfig, array[]::text[])
          @> array['search_path=pg_catalog, public, pg_temp'];

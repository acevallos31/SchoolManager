-- Validacion Migracion 022: portal responsable de solo lectura.
-- Contrato: esta consulta debe devolver cero filas. Cualquier fila es un hallazgo.
-- El inventario de funciones se declara una sola vez para evitar divergencias entre
-- chequeos de existencia, seguridad, grants y guard de responsable.

with funciones(firma, tipo, requiere_guard) as (
  values
    ('usuario_es_responsable_financiero_del_alumno(uuid)', 'helper', false),
    ('rpc_mis_alumnos_responsable()', 'rpc', false),
    ('rpc_resumen_financiero_responsable(uuid,uuid)', 'rpc', true),
    ('rpc_cargos_responsable(uuid,uuid)', 'rpc', true),
    ('rpc_pagos_responsable(uuid,uuid)', 'rpc', true),
    ('rpc_pago_aplicaciones_responsable(uuid,uuid)', 'rpc', true)
), catalogo as (
  select f.*, to_regprocedure('public.' || f.firma) as oid
  from funciones f
), proc_info as (
  select c.*, p.prosecdef, p.proconfig, pg_get_functiondef(p.oid) as definicion
  from catalogo c
  left join pg_proc p on p.oid = c.oid
), hallazgos as (
  -- Registro de migracion: exactamente una fila con el nombre canonico.
  select '022_no_registrada_o_nombre_incorrecto'::text as diagnostico, '022'::text as objeto
  where (select count(*) from public.schema_migrations
         where version = '022' and nombre = 'portal_responsable_lectura') <> 1

  union all
  select '022_duplicada', '022'
  where (select count(*) from public.schema_migrations where version = '022') > 1

  -- Existencia y configuracion segura de todas las funciones del bloque.
  union all
  select case when tipo = 'helper' then 'helper_faltante' else 'rpc_faltante' end, firma
  from proc_info
  where oid is null

  union all
  select 'funcion_portal_insegura', firma
  from proc_info
  where oid is not null
    and (not prosecdef
      or not coalesce(proconfig, array[]::text[])
             @> array['search_path=pg_catalog, public, pg_temp'])

  -- El helper es interno: solo service_role debe ejecutarlo.
  union all
  select 'helper_expuesto_indebidamente', rol
  from proc_info
  cross join unnest(array['public','anon','authenticated']) as x(rol)
  where tipo = 'helper'
    and oid is not null
    and has_function_privilege(rol, oid, 'EXECUTE')

  union all
  select 'helper_sin_grant_requerido', 'service_role'
  from proc_info
  where tipo = 'helper'
    and oid is not null
    and not has_function_privilege('service_role', oid, 'EXECUTE')

  -- Las RPC del portal se exponen a authenticated/service_role, nunca public/anon.
  union all
  select 'rpc_expuesta_indebidamente', rol || ':' || firma
  from proc_info
  cross join unnest(array['public','anon']) as x(rol)
  where tipo = 'rpc'
    and oid is not null
    and has_function_privilege(rol, oid, 'EXECUTE')

  union all
  select 'rpc_sin_grant_requerido', rol || ':' || firma
  from proc_info
  cross join unnest(array['authenticated','service_role']) as x(rol)
  where tipo = 'rpc'
    and oid is not null
    and not has_function_privilege(rol, oid, 'EXECUTE')

  -- Identidad y aislamiento: helper por auth.uid() y RPC financieras por guard.
  union all
  select 'helper_sin_auth_uid', firma
  from proc_info
  where tipo = 'helper'
    and oid is not null
    and definicion not like '%auth.uid()%'

  union all
  select 'rpc_sin_guard_responsable', firma
  from proc_info
  where requiere_guard
    and oid is not null
    and definicion not like '%usuario_es_responsable_financiero_del_alumno%'
)
select diagnostico, objeto
from hallazgos;

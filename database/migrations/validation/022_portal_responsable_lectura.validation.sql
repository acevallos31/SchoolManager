-- Validacion Migracion 022: portal responsable de solo lectura.
-- Contrato: cada consulta debe devolver cero filas. Cualquier fila es un hallazgo.
-- Esta validacion describe el estado inmediatamente posterior a 022 y usa OIDs
-- de to_regprocedure para no fallar por resolucion de nombres si algun objeto falta.

-- 1. La migracion debe estar registrada exactamente una vez y con el nombre esperado.
select '022_no_registrada_o_nombre_incorrecto' as error
where (
  select count(*)
  from public.schema_migrations
  where version = '022' and nombre = 'portal_responsable_lectura'
) <> 1;

select '022_duplicada' as error
from public.schema_migrations
where version = '022'
group by version
having count(*) > 1;

-- 2. Helper interno y firmas RPC exactas presentes.
select 'usuario_es_responsable_financiero_del_alumno(uuid)' as helper_faltante
where to_regprocedure('public.usuario_es_responsable_financiero_del_alumno(uuid)') is null;

select esperado.firma as rpc_faltante
from (values
  ('rpc_mis_alumnos_responsable()'),
  ('rpc_resumen_financiero_responsable(uuid,uuid)'),
  ('rpc_cargos_responsable(uuid,uuid)'),
  ('rpc_pagos_responsable(uuid,uuid)'),
  ('rpc_pago_aplicaciones_responsable(uuid,uuid)')
) esperado(firma)
where to_regprocedure('public.' || esperado.firma) is null;

-- 3. Helper y RPCs deben ser SECURITY DEFINER con search_path fijo.
select esperado.firma as funcion_portal_insegura
from (values
  ('usuario_es_responsable_financiero_del_alumno(uuid)'),
  ('rpc_mis_alumnos_responsable()'),
  ('rpc_resumen_financiero_responsable(uuid,uuid)'),
  ('rpc_cargos_responsable(uuid,uuid)'),
  ('rpc_pagos_responsable(uuid,uuid)'),
  ('rpc_pago_aplicaciones_responsable(uuid,uuid)')
) esperado(firma)
join pg_proc p on p.oid = to_regprocedure('public.' || esperado.firma)
where not p.prosecdef
   or not coalesce(p.proconfig, array[]::text[])
          @> array['search_path=pg_catalog, public, pg_temp'];

-- 4. El helper es interno: public/anon/authenticated no deben ejecutarlo.
select roles.rol as helper_expuesto_indebidamente
from (values ('public'), ('anon'), ('authenticated')) roles(rol)
where has_function_privilege(
  roles.rol,
  to_regprocedure('public.usuario_es_responsable_financiero_del_alumno(uuid)'),
  'EXECUTE') is true;

select 'service_role' as helper_sin_grant_requerido
where to_regprocedure('public.usuario_es_responsable_financiero_del_alumno(uuid)') is not null
  and has_function_privilege(
    'service_role',
    to_regprocedure('public.usuario_es_responsable_financiero_del_alumno(uuid)'),
    'EXECUTE') is not true;

-- 5. RPCs del portal: nada para public/anon; authenticated y service_role si ejecutan.
select roles.rol, fn.firma as rpc_expuesta_indebidamente
from (values ('public'), ('anon')) roles(rol)
cross join (values
  ('rpc_mis_alumnos_responsable()'),
  ('rpc_resumen_financiero_responsable(uuid,uuid)'),
  ('rpc_cargos_responsable(uuid,uuid)'),
  ('rpc_pagos_responsable(uuid,uuid)'),
  ('rpc_pago_aplicaciones_responsable(uuid,uuid)')
) fn(firma)
where has_function_privilege(
  roles.rol,
  to_regprocedure('public.' || fn.firma),
  'EXECUTE') is true;

select roles.rol, fn.firma as rpc_sin_grant_requerido
from (values ('authenticated'), ('service_role')) roles(rol)
cross join (values
  ('rpc_mis_alumnos_responsable()'),
  ('rpc_resumen_financiero_responsable(uuid,uuid)'),
  ('rpc_cargos_responsable(uuid,uuid)'),
  ('rpc_pagos_responsable(uuid,uuid)'),
  ('rpc_pago_aplicaciones_responsable(uuid,uuid)')
) fn(firma)
where to_regprocedure('public.' || fn.firma) is not null
  and has_function_privilege(
    roles.rol,
    to_regprocedure('public.' || fn.firma),
    'EXECUTE') is not true;

-- 6. El helper debe seguir resolviendo la identidad autenticada por auth.uid().
select 'helper_sin_auth_uid' as error
from pg_proc p
join pg_namespace n on n.oid = p.pronamespace
where n.nspname = 'public'
  and p.oid = to_regprocedure('public.usuario_es_responsable_financiero_del_alumno(uuid)')
  and pg_get_functiondef(p.oid) not like '%auth.uid()%';

-- 7. Las RPC de lectura financiera deben conservar el guard de responsable.
select esperado.firma as rpc_sin_guard_responsable
from (values
  ('rpc_resumen_financiero_responsable(uuid,uuid)'),
  ('rpc_cargos_responsable(uuid,uuid)'),
  ('rpc_pagos_responsable(uuid,uuid)'),
  ('rpc_pago_aplicaciones_responsable(uuid,uuid)')
) esperado(firma)
join pg_proc p on p.oid = to_regprocedure('public.' || esperado.firma)
where pg_get_functiondef(p.oid)
        not like '%usuario_es_responsable_financiero_del_alumno%';

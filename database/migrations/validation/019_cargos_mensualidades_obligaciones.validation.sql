-- Validacion Migracion 019: cargos / mensualidades / obligaciones generadas.
-- Contrato: cada consulta debe devolver cero filas. Cualquier fila es un hallazgo.
-- Esta validacion describe el estado inmediatamente posterior a 019.

-- 1. La migracion debe estar registrada exactamente una vez y con el nombre esperado.
select '019_no_registrada_o_nombre_incorrecto' as error
where (
  select count(*)
  from public.schema_migrations
  where version = '019' and nombre = 'cargos_mensualidades_obligaciones'
) <> 1;

select '019_duplicada' as error
from public.schema_migrations
where version = '019'
group by version
having count(*) > 1;

-- 2. matriculas.plan_pago_id debe existir como UUID y referenciar planes_pago(id).
select 'matriculas_plan_pago_id_faltante_o_tipo_incorrecto' as error
where not exists (
  select 1
  from information_schema.columns
  where table_schema = 'public'
    and table_name = 'matriculas'
    and column_name = 'plan_pago_id'
    and data_type = 'uuid'
);

select 'fk_matriculas_plan_pago_faltante' as error
where not exists (
  select 1
  from pg_constraint c
  join pg_class t on t.oid = c.conrelid
  join pg_namespace n on n.oid = t.relnamespace
  join pg_class rt on rt.oid = c.confrelid
  join pg_namespace rn on rn.oid = rt.relnamespace
  where n.nspname = 'public'
    and t.relname = 'matriculas'
    and c.contype = 'f'
    and rn.nspname = 'public'
    and rt.relname = 'planes_pago'
    and pg_get_constraintdef(c.oid) like '%(plan_pago_id)%REFERENCES public.planes_pago(id)%'
);

-- 3. Tabla cargos y controles estructurales principales.
select 'tabla_cargos_faltante' as error
where to_regclass('public.cargos') is null;

select 'cargos_rls_inactiva' as error
from pg_class t
join pg_namespace n on n.oid = t.relnamespace
where n.nspname = 'public'
  and t.relname = 'cargos'
  and not t.relrowsecurity;

select esperado.objeto as objeto_cargos_faltante
from (values
  ('ix_cargos_matricula'),
  ('ix_cargos_alumno_estado'),
  ('ix_cargos_concepto_id')
) esperado(objeto)
where to_regclass('public.' || esperado.objeto) is null;

select esperado.constraint_name as constraint_cargos_faltante
from (values
  ('fk_cargos_alumno_institucion'),
  ('ck_cargos_anulacion_coherente')
) esperado(constraint_name)
where not exists (
  select 1
  from pg_constraint c
  join pg_class t on t.oid = c.conrelid
  join pg_namespace n on n.oid = t.relnamespace
  where n.nspname = 'public'
    and t.relname = 'cargos'
    and c.conname = esperado.constraint_name
);

-- 4. Triggers de coherencia de institucion/plan y cargo presentes.
select esperado.trigger_name as trigger_faltante
from (values
  ('trg_matriculas_plan_institucion_before'),
  ('trg_cargos_coherencia_before')
) esperado(trigger_name)
where not exists (
  select 1
  from pg_trigger t
  where t.tgname = esperado.trigger_name
    and not t.tgisinternal
);

select esperado.firma as funcion_trigger_faltante
from (values
  ('trg_matriculas_plan_institucion()'),
  ('trg_cargos_coherencia()')
) esperado(firma)
where to_regprocedure('public.' || esperado.firma) is null;

-- 5. Helpers de trigger no deben quedar expuestos a clientes.
select roles.rol, fn.firma as trigger_auxiliar_expuesto
from (values ('anon'), ('authenticated')) roles(rol)
cross join (values
  ('trg_matriculas_plan_institucion()'),
  ('trg_cargos_coherencia()')
) fn(firma)
where to_regprocedure('public.' || fn.firma) is not null
  and has_function_privilege(roles.rol, 'public.' || fn.firma, 'EXECUTE');

-- 6. Permisos propios de cargos presentes y asignados al admin activo.
select esperado.codigo as permiso_faltante
from (values
  ('academico.cargos.ver'),
  ('academico.cargos.generar'),
  ('academico.cargos.anular')
) esperado(codigo)
where not exists (
  select 1 from public.permisos p where p.codigo = esperado.codigo
);

select esperado.codigo as permiso_admin_faltante
from (values
  ('academico.cargos.ver'),
  ('academico.cargos.generar'),
  ('academico.cargos.anular')
) esperado(codigo)
where not exists (
  select 1
  from public.roles_permisos rp
  join public.roles r on r.id = rp.rol_id
  join public.permisos p on p.id = rp.permiso_id
  where r.codigo = 'admin'
    and r.activo = true
    and p.codigo = esperado.codigo
);

-- 7. Firmas RPC exactas de 019 presentes.
select esperado.firma as rpc_faltante
from (values
  ('rpc_listar_cargos_matricula(uuid,uuid)'),
  ('rpc_listar_cargos_alumno(uuid,uuid)'),
  ('rpc_resumen_financiero_alumno(uuid,uuid)'),
  ('rpc_asignar_plan_pago_matricula(uuid,uuid,uuid)'),
  ('rpc_generar_cargos_matricula(uuid,uuid)'),
  ('rpc_anular_cargo(uuid,text,uuid)')
) esperado(firma)
where to_regprocedure('public.' || esperado.firma) is null;

-- 8. Todas las RPC deben ser SECURITY DEFINER y fijar search_path.
select esperado.firma as rpc_cargos_insegura
from (values
  ('rpc_listar_cargos_matricula(uuid,uuid)'),
  ('rpc_listar_cargos_alumno(uuid,uuid)'),
  ('rpc_resumen_financiero_alumno(uuid,uuid)'),
  ('rpc_asignar_plan_pago_matricula(uuid,uuid,uuid)'),
  ('rpc_generar_cargos_matricula(uuid,uuid)'),
  ('rpc_anular_cargo(uuid,text,uuid)')
) esperado(firma)
join pg_proc p on p.oid = to_regprocedure('public.' || esperado.firma)
where not p.prosecdef
   or not coalesce(p.proconfig, array[]::text[])
          @> array['search_path=pg_catalog, public, pg_temp'];

-- 9. RPCs: nada para public/anon; authenticated y service_role si ejecutan.
select roles.rol, fn.firma as rpc_expuesta_indebidamente
from (values ('public'), ('anon')) roles(rol)
cross join (values
  ('rpc_listar_cargos_matricula(uuid,uuid)'),
  ('rpc_listar_cargos_alumno(uuid,uuid)'),
  ('rpc_resumen_financiero_alumno(uuid,uuid)'),
  ('rpc_asignar_plan_pago_matricula(uuid,uuid,uuid)'),
  ('rpc_generar_cargos_matricula(uuid,uuid)'),
  ('rpc_anular_cargo(uuid,text,uuid)')
) fn(firma)
where to_regprocedure('public.' || fn.firma) is not null
  and has_function_privilege(roles.rol, 'public.' || fn.firma, 'EXECUTE');

select roles.rol, fn.firma as rpc_sin_grant_requerido
from (values ('authenticated'), ('service_role')) roles(rol)
cross join (values
  ('rpc_listar_cargos_matricula(uuid,uuid)'),
  ('rpc_listar_cargos_alumno(uuid,uuid)'),
  ('rpc_resumen_financiero_alumno(uuid,uuid)'),
  ('rpc_asignar_plan_pago_matricula(uuid,uuid,uuid)'),
  ('rpc_generar_cargos_matricula(uuid,uuid)'),
  ('rpc_anular_cargo(uuid,text,uuid)')
) fn(firma)
where to_regprocedure('public.' || fn.firma) is not null
  and not has_function_privilege(roles.rol, 'public.' || fn.firma, 'EXECUTE');

-- 10. La tabla cargos no debe dar acceso directo a anon/authenticated.
select roles.rol, privilegios.privilegio as privilegio_tabla_indebido
from (values ('anon'), ('authenticated')) roles(rol)
cross join (values
  ('SELECT'), ('INSERT'), ('UPDATE'), ('DELETE'), ('TRUNCATE'), ('REFERENCES'), ('TRIGGER')
) privilegios(privilegio)
where to_regclass('public.cargos') is not null
  and has_table_privilege(roles.rol, 'public.cargos', privilegios.privilegio);

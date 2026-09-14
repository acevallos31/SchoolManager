-- Bloque 042 - validacion posterior al rollout RBAC 028..039
-- Ejecutar DESPUES de aplicar, en orden, las migraciones 028 a 039.
-- Este script es de solo lectura: no modifica datos ni esquema.

-- 1) Cadena de migraciones esperada.
select version, nombre
from public.schema_migrations
where version between '028' and '039'
order by version;

-- Debe devolver exactamente 12 filas: 028..039.
with esperadas(version) as (
  values
    ('028'),('029'),('030'),('031'),('032'),('033'),
    ('034'),('035'),('036'),('037'),('038'),('039')
), faltantes as (
  select e.version
  from esperadas e
  left join public.schema_migrations sm on sm.version = e.version
  where sm.version is null
)
select * from faltantes order by version;
-- Resultado esperado: 0 filas.

-- 2) Rol protegido de plataforma.
select id, codigo, nombre, tipo, institucion_id, protegido, activo
from public.roles
where codigo = 'platform_admin';
-- Resultado esperado: exactamente 1 fila, tipo=plataforma,
-- institucion_id IS NULL, protegido=true, activo=true.

-- 3) Permisos exclusivos de plataforma.
select codigo, ambito, delegable, riesgo, estado, visible_en_roles
from public.permisos
where codigo like 'platform.%'
order by codigo;
-- Resultado esperado: todos ambito=plataforma y delegable=false.

-- 4) Plantillas globales.
select codigo, tipo, institucion_id, protegido, activo, plantilla_version
from public.roles
where tipo = 'plantilla'
order by codigo;
-- Deben existir al menos:
-- school_admin, school_staff, academic_coordinator, finance_operator,
-- teacher, parent, student, demo_viewer, support_agent.

-- 5) Aliases internos no deben ser delegables ni visibles en editor.
select codigo, delegable, visible_en_roles, estado
from public.permisos
where codigo like 'configuracion.ciclos.%'
   or codigo like 'configuracion.periodos_matricula.%'
   or codigo like 'configuracion.grados.%'
   or codigo like 'configuracion.jornadas.%'
   or codigo like 'configuracion.secciones.%'
order by codigo;
-- Resultado esperado: delegable=false y visible_en_roles=false.

-- 6) Helper estricto y snapshot de Seguridad y acceso deben existir.
select
  to_regprocedure('public.usuario_tiene_permiso_institucional_estricto(text,uuid)') as helper_estricto,
  to_regprocedure('public.rpc_obtener_seguridad_acceso(uuid)') as snapshot_seguridad,
  to_regprocedure('public.usuario_tiene_permiso_actual(text,uuid)') as helper_actual;
-- Ninguna columna debe ser NULL.

-- 7) Validar que 039 contiene el puente canonico esperado.
select pg_get_functiondef(
  'public.usuario_tiene_permiso_actual(text,uuid)'::regprocedure
) as definicion_usuario_tiene_permiso_actual;
-- Debe contener academico.ciclos.* y academico.estructura.*.

-- 8) Políticas RLS relevantes.
select schemaname, tablename, policyname, roles, cmd
from pg_policies
where schemaname = 'public'
  and tablename in ('roles','roles_permisos','usuarios_roles','permisos')
order by tablename, policyname;

-- 9) Auditoría RBAC.
select to_regclass('public.seguridad_auditoria') as seguridad_auditoria;
-- Debe ser public.seguridad_auditoria.

-- 10) Invariantes de assignments de plataforma.
select count(*) as asignaciones_platform_admin_invalidas
from public.usuarios_roles ur
join public.roles r on r.id = ur.rol_id
where r.codigo = 'platform_admin'
  and ur.institucion_id is not null;
-- Resultado esperado: 0.

-- 11) Antes del bootstrap inicial debe haber cero Superadministradores activos.
-- Despues del bootstrap debe haber exactamente uno o mas.
select count(*) as superadministradores_activos
from public.usuarios_roles ur
join public.roles r on r.id = ur.rol_id
join public.usuarios u on u.id = ur.usuario_id
where ur.activo
  and ur.institucion_id is null
  and r.activo
  and r.tipo = 'plataforma'
  and r.codigo = 'platform_admin'
  and u.activo;

-- 12) Resumen final: debe devolver PASS si la estructura mínima está correcta.
with checks as (
  select
    (select count(*) = 12
       from public.schema_migrations
       where version between '028' and '039') as migraciones_ok,
    (select count(*) = 1
       from public.roles
       where codigo='platform_admin'
         and tipo='plataforma'
         and institucion_id is null
         and protegido and activo) as platform_admin_ok,
    (select count(*) = 6
       from public.permisos
       where codigo like 'platform.%'
         and ambito='plataforma'
         and delegable=false
         and estado='vigente') as permisos_platform_ok,
    (to_regprocedure('public.usuario_tiene_permiso_institucional_estricto(text,uuid)') is not null) as helper_estricto_ok,
    (to_regprocedure('public.rpc_obtener_seguridad_acceso(uuid)') is not null) as snapshot_ok,
    (to_regprocedure('public.usuario_tiene_permiso_actual(text,uuid)') is not null) as helper_actual_ok
)
select *,
  case when migraciones_ok
          and platform_admin_ok
          and permisos_platform_ok
          and helper_estricto_ok
          and snapshot_ok
          and helper_actual_ok
       then 'PASS'
       else 'FAIL'
  end as resultado
from checks;

-- Bloque 042 - preflight de produccion antes de aplicar RBAC 028..039
-- SOLO LECTURA. Ejecutar primero en el SQL Manager.

-- A) Confirmar punto de partida.
select version, nombre
from public.schema_migrations
order by version;

-- B) Debe existir 027 y no debe existir ninguna 028..039 antes del rollout.
select
  exists(select 1 from public.schema_migrations where version='027') as tiene_027,
  exists(select 1 from public.schema_migrations where version between '028' and '039') as ya_hay_028_039;
-- Esperado antes del rollout: tiene_027=true, ya_hay_028_039=false.

-- C) Conteo base de objetos RBAC legacy.
select
  (select count(*) from public.roles) as roles,
  (select count(*) from public.permisos) as permisos,
  (select count(*) from public.usuarios_roles where activo) as asignaciones_activas,
  (select count(*) from public.instituciones where activo) as instituciones_activas,
  (select count(*) from public.usuarios where activo) as usuarios_activos;

-- D) Asignaciones activas actuales por rol y scope.
select r.codigo,
       case when ur.institucion_id is null then 'global' else 'institucion' end as scope,
       count(*) as cantidad
from public.usuarios_roles ur
join public.roles r on r.id = ur.rol_id
where ur.activo
  and r.activo
group by r.codigo, case when ur.institucion_id is null then 'global' else 'institucion' end
order by r.codigo, scope;

-- E) Validar que las columnas nuevas de 028 aun no existan si este es el primer rollout.
select
  exists(
    select 1 from information_schema.columns
    where table_schema='public' and table_name='roles' and column_name='tipo'
  ) as roles_tipo_existe,
  exists(
    select 1 from information_schema.columns
    where table_schema='public' and table_name='permisos' and column_name='ambito'
  ) as permisos_ambito_existe;

-- F) Si alguno de estos checks no coincide con el estado esperado, NO continuar
-- hasta revisar si hubo una aplicacion parcial previa.

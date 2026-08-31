-- Las consultas de diagnostico deben devolver cero filas, salvo el registro final.
select esperado.firma as rpc_faltante from (values
 ('rpc_actualizar_ciclo_escolar(uuid,text,date,date,boolean,text)'),
 ('rpc_crear_periodo_matricula(uuid,text,text,date,date)'),
 ('rpc_actualizar_periodo_matricula(uuid,text,text,date,date,boolean)')
) esperado(firma) where to_regprocedure('public.'||esperado.firma) is null;

select p.oid::regprocedure as validacion_legacy_presente
from pg_proc p join pg_namespace n on n.oid=p.pronamespace
where n.nspname='public'
  and p.proname in ('rpc_actualizar_ciclo_escolar','rpc_crear_periodo_matricula','rpc_actualizar_periodo_matricula')
  and (p.prosrc ilike '%dentro de las fechas del ciclo%'
    or p.prosrc ilike '%excluye periodos de matricula%'
    or p.prosrc ilike '%p_fecha_inicio<c.fecha_inicio%'
    or p.prosrc ilike '%p_fecha_fin>c.fecha_fin%');

select version,nombre from public.schema_migrations where version='015';

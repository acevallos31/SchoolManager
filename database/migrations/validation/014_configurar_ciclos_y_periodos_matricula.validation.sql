select esperado.firma as rpc_faltante from (values
 ('rpc_listar_ciclos_escolares(uuid)'),('rpc_crear_ciclo_escolar(text,date,date,uuid)'),
 ('rpc_actualizar_ciclo_escolar(uuid,text,date,date,boolean,text)'),
 ('rpc_desactivar_ciclo_escolar(uuid,text)'),('rpc_reactivar_ciclo_escolar(uuid)'),
 ('rpc_listar_periodos_matricula(uuid)'),('rpc_crear_periodo_matricula(uuid,text,text,date,date)'),
 ('rpc_actualizar_periodo_matricula(uuid,text,text,date,date,boolean)'),
 ('rpc_desactivar_periodo_matricula(uuid)'),('rpc_reactivar_periodo_matricula(uuid)')
) esperado(firma) where to_regprocedure('public.'||esperado.firma) is null;
select esperado.codigo as permiso_faltante from (values
 ('configuracion.ciclos.ver'),('configuracion.ciclos.crear'),
 ('configuracion.ciclos.editar'),('configuracion.ciclos.desactivar'),
 ('configuracion.periodos_matricula.ver'),('configuracion.periodos_matricula.crear'),
 ('configuracion.periodos_matricula.editar'),('configuracion.periodos_matricula.desactivar')
) esperado(codigo)
where not exists(select 1 from public.permisos p where p.codigo=esperado.codigo);
select version,nombre from public.schema_migrations where version='014';

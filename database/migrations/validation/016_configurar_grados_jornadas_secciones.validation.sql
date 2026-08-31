-- Diagnosticos: cero filas, salvo registro final.
select esperado.codigo as permiso_faltante from (values
 ('configuracion.grados.ver'),('configuracion.grados.crear'),('configuracion.grados.editar'),('configuracion.grados.desactivar'),
 ('configuracion.jornadas.ver'),('configuracion.jornadas.crear'),('configuracion.jornadas.editar'),('configuracion.jornadas.desactivar'),
 ('configuracion.secciones.ver'),('configuracion.secciones.crear'),('configuracion.secciones.editar'),('configuracion.secciones.desactivar'))esperado(codigo)
where not exists(select 1 from public.permisos p where p.codigo=esperado.codigo);
select esperado.firma as rpc_faltante from (values
 ('rpc_listar_grados(uuid)'),('rpc_crear_grado(text,integer,uuid)'),('rpc_actualizar_grado(uuid,text,integer,uuid)'),('rpc_desactivar_grado(uuid,uuid)'),('rpc_reactivar_grado(uuid,uuid)'),
 ('rpc_listar_jornadas(uuid)'),('rpc_crear_jornada(text,uuid)'),('rpc_actualizar_jornada(uuid,text,uuid)'),('rpc_desactivar_jornada(uuid,uuid)'),('rpc_reactivar_jornada(uuid,uuid)'),
 ('rpc_listar_secciones(uuid,uuid)'),('rpc_crear_seccion(uuid,uuid,uuid,uuid,text,integer)'),('rpc_actualizar_seccion(uuid,uuid,uuid,uuid,text,integer,uuid)'),('rpc_desactivar_seccion(uuid,text,uuid)'),('rpc_reactivar_seccion(uuid,uuid)'))esperado(firma)
where to_regprocedure('public.'||esperado.firma) is null;
select 'RLS inactiva: '||c.relname error from pg_class c where c.oid in('public.grados'::regclass,'public.jornadas'::regclass,'public.secciones'::regclass) and not c.relrowsecurity;
select indice_faltante from (values ('ux_grados_nombre_normalizado'),('ux_jornadas_nombre_normalizado'),('ux_secciones_contexto_jornada_nombre'),('ux_secciones_contexto_sin_jornada_nombre')) esperado(indice_faltante)
where not exists(select 1 from pg_indexes i where i.schemaname='public' and i.indexname=esperado.indice_faltante);
select firma_legacy from (values ('rpc_actualizar_seccion(uuid,uuid,uuid,uuid,text,integer)'),('rpc_desactivar_seccion(uuid,text)'),('rpc_reactivar_seccion(uuid)')) esperado(firma_legacy)
where to_regprocedure('public.'||esperado.firma_legacy) is not null;
select version,nombre from public.schema_migrations where version='016';

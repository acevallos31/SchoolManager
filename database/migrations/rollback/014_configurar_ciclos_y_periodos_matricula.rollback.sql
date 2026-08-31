begin;
drop function if exists public.rpc_reactivar_periodo_matricula(uuid);
drop function if exists public.rpc_desactivar_periodo_matricula(uuid);
drop function if exists public.rpc_actualizar_periodo_matricula(uuid,text,text,date,date,boolean);
drop function if exists public.rpc_crear_periodo_matricula(uuid,text,text,date,date);
drop function if exists public.rpc_listar_periodos_matricula(uuid);
drop function if exists public.rpc_reactivar_ciclo_escolar(uuid);
drop function if exists public.rpc_desactivar_ciclo_escolar(uuid,text);
drop function if exists public.rpc_actualizar_ciclo_escolar(uuid,text,date,date,boolean,text);
drop function if exists public.rpc_crear_ciclo_escolar(text,date,date,uuid);
drop function if exists public.rpc_listar_ciclos_escolares(uuid);
drop function if exists public.resolver_institucion_operacion(uuid);
delete from public.roles_permisos rp using public.permisos p where rp.permiso_id=p.id
 and (p.codigo like 'configuracion.ciclos.%' or p.codigo like 'configuracion.periodos_matricula.%');
delete from public.permisos where codigo like 'configuracion.ciclos.%'
 or codigo like 'configuracion.periodos_matricula.%';
delete from public.schema_migrations where version='014';
commit;

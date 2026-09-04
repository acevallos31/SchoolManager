-- Rollback 017: retira la gestion RPC de responsables. Conserva las tablas
-- (004) y la RLS/grant select (009). Despues de este rollback vuelve a no ser
-- posible crear/editar/desactivar/vincular responsables desde cliente.
begin;

drop function if exists public.crear_o_reutilizar_persona_con_documento(text,text,text,text,text,text,text);
drop function if exists public.crear_responsable(uuid,uuid);
drop function if exists public.rpc_crear_responsable_con_documento(uuid,text,text,text,text,text,text);
drop function if exists public.rpc_crear_responsable_para_persona(uuid,uuid);
drop function if exists public.editar_datos_persona(uuid,text,text,text,text);
drop function if exists public.rpc_editar_responsable(uuid,text,text,text,text);
drop function if exists public.inactivar_responsable(uuid,text);
drop function if exists public.rpc_inactivar_responsable(uuid,text);
drop function if exists public.reactivar_responsable(uuid);
drop function if exists public.rpc_reactivar_responsable(uuid);
drop function if exists public.vincular_alumno_responsable(uuid,uuid,text,boolean,boolean);
drop function if exists public.rpc_vincular_alumno_responsable(uuid,uuid,text,boolean,boolean);
drop function if exists public.editar_vinculo_responsable(uuid,text,boolean,boolean);
drop function if exists public.rpc_editar_vinculo_responsable(uuid,text,boolean,boolean);
drop function if exists public.desactivar_vinculo_responsable(uuid,text);
drop function if exists public.rpc_desactivar_vinculo_responsable(uuid,text);
drop function if exists public.reactivar_vinculo_responsable(uuid);
drop function if exists public.rpc_reactivar_vinculo_responsable(uuid);

delete from public.schema_migrations where version = '017';

commit;
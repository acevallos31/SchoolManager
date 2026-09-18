-- Migracion 046 - hardening de SECURITY DEFINER expuestas a authenticated.
-- Deuda #15 / issue #109.
--
-- Arquitectura vigente: Angular y clientes moviles consumen API .NET para negocio.
-- Supabase directo en clientes queda reservado a Auth.
-- Por tanto, las RPC public.rpc_* de negocio no requieren EXECUTE directo de
-- authenticated. Los helpers usuario_* usados por RLS se preservan sin cambios.
--
-- Inventario base de produccion antes de 046:
--   93 SECURITY DEFINER ejecutables por authenticated
--   84 RPC public.rpc_* de negocio -> se revocan explicitamente
--    9 helpers usuario_* usados por RLS -> se preservan
--
-- No cambia SECURITY DEFINER/INVOKER, cuerpos, RLS ni service_role.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version='045') then
    raise exception 'La migracion 046 requiere la 045 aplicada previamente.';
  end if;
end
$$;

revoke execute on function public.rpc_actualizar_ciclo_escolar(uuid,text,date,date,boolean,text) from authenticated;
revoke execute on function public.rpc_actualizar_concepto_financiero(uuid,text,numeric,text,uuid) from authenticated;
revoke execute on function public.rpc_actualizar_grado(uuid,text,integer,uuid) from authenticated;
revoke execute on function public.rpc_actualizar_institucion(uuid,text,text,text,text,text,text,boolean,boolean,boolean,text[]) from authenticated;
revoke execute on function public.rpc_actualizar_jornada(uuid,text,uuid) from authenticated;
revoke execute on function public.rpc_actualizar_multiples_instituciones(boolean) from authenticated;
revoke execute on function public.rpc_actualizar_periodo_matricula(uuid,text,text,date,date,boolean) from authenticated;
revoke execute on function public.rpc_actualizar_plan_pago(uuid,text,text,jsonb,uuid) from authenticated;
revoke execute on function public.rpc_actualizar_seccion(uuid,uuid,uuid,uuid,text,integer,uuid) from authenticated;
revoke execute on function public.rpc_anular_cargo(uuid,text,uuid) from authenticated;
revoke execute on function public.rpc_anular_pago(uuid,text,uuid) from authenticated;
revoke execute on function public.rpc_asignar_plan_pago_matricula(uuid,uuid,uuid) from authenticated;
revoke execute on function public.rpc_asignar_rol_institucional(uuid,uuid) from authenticated;
revoke execute on function public.rpc_asignar_rol_usuario(uuid,text,uuid) from authenticated;
revoke execute on function public.rpc_cambiar_estado_grado(uuid,boolean,uuid) from authenticated;
revoke execute on function public.rpc_cambiar_estado_jornada(uuid,boolean,uuid) from authenticated;
revoke execute on function public.rpc_cambiar_estado_matricula(uuid,text,text) from authenticated;
revoke execute on function public.rpc_cargos_responsable(uuid,uuid) from authenticated;
revoke execute on function public.rpc_clonar_plantilla_rol(uuid,text,text,text,text) from authenticated;
revoke execute on function public.rpc_crear_alumno_nueva_persona(uuid,text,text,date,text,text) from authenticated;
revoke execute on function public.rpc_crear_alumno_nueva_persona_con_documento(uuid,text,text,text,text,date,text,text) from authenticated;
revoke execute on function public.rpc_crear_alumno_para_persona(uuid,uuid,date,text,text) from authenticated;
revoke execute on function public.rpc_crear_ciclo_escolar(text,date,date,uuid) from authenticated;
revoke execute on function public.rpc_crear_concepto_financiero(text,numeric,text,uuid) from authenticated;
revoke execute on function public.rpc_crear_grado(text,integer,uuid) from authenticated;
revoke execute on function public.rpc_crear_institucion(text,text,text,text,text,text,boolean,boolean,boolean,text[]) from authenticated;
revoke execute on function public.rpc_crear_jornada(text,uuid) from authenticated;
revoke execute on function public.rpc_crear_periodo_matricula(uuid,text,text,date,date) from authenticated;
revoke execute on function public.rpc_crear_plan_pago(text,text,jsonb,uuid) from authenticated;
revoke execute on function public.rpc_crear_responsable_con_documento(uuid,text,text,text,text,text,text) from authenticated;
revoke execute on function public.rpc_crear_responsable_para_persona(uuid,uuid) from authenticated;
revoke execute on function public.rpc_crear_rol_institucional(uuid,text,text,text) from authenticated;
revoke execute on function public.rpc_crear_seccion(uuid,uuid,uuid,uuid,text,integer) from authenticated;
revoke execute on function public.rpc_desactivar_alumno(uuid,text) from authenticated;
revoke execute on function public.rpc_desactivar_ciclo_escolar(uuid,text) from authenticated;
revoke execute on function public.rpc_desactivar_concepto_financiero(uuid,text,uuid) from authenticated;
revoke execute on function public.rpc_desactivar_grado(uuid,uuid) from authenticated;
revoke execute on function public.rpc_desactivar_jornada(uuid,uuid) from authenticated;
revoke execute on function public.rpc_desactivar_periodo_matricula(uuid) from authenticated;
revoke execute on function public.rpc_desactivar_plan_pago(uuid,text,uuid) from authenticated;
revoke execute on function public.rpc_desactivar_rol_institucional(uuid,text) from authenticated;
revoke execute on function public.rpc_desactivar_rol_usuario(uuid,text) from authenticated;
revoke execute on function public.rpc_desactivar_seccion(uuid,text,uuid) from authenticated;
revoke execute on function public.rpc_desactivar_vinculo_responsable(uuid,text) from authenticated;
revoke execute on function public.rpc_editar_responsable(uuid,text,text,text,text) from authenticated;
revoke execute on function public.rpc_editar_rol_institucional(uuid,text,text) from authenticated;
revoke execute on function public.rpc_editar_vinculo_responsable(uuid,text,boolean,boolean) from authenticated;
revoke execute on function public.rpc_generar_cargos_matricula(uuid,uuid) from authenticated;
revoke execute on function public.rpc_inactivar_responsable(uuid,text) from authenticated;
revoke execute on function public.rpc_listar_cargos_alumno(uuid,uuid) from authenticated;
revoke execute on function public.rpc_listar_cargos_matricula(uuid,uuid) from authenticated;
revoke execute on function public.rpc_listar_ciclos_escolares(uuid) from authenticated;
revoke execute on function public.rpc_listar_conceptos_financieros(uuid,boolean) from authenticated;
revoke execute on function public.rpc_listar_grados(uuid) from authenticated;
revoke execute on function public.rpc_listar_jornadas(uuid) from authenticated;
revoke execute on function public.rpc_listar_pagos_alumno(uuid,uuid) from authenticated;
revoke execute on function public.rpc_listar_periodos_matricula(uuid) from authenticated;
revoke execute on function public.rpc_listar_planes_pago(uuid,boolean) from authenticated;
revoke execute on function public.rpc_listar_secciones(uuid,uuid) from authenticated;
revoke execute on function public.rpc_matricular_alumno(uuid,uuid,uuid) from authenticated;
revoke execute on function public.rpc_mis_alumnos_responsable() from authenticated;
revoke execute on function public.rpc_obtener_aplicaciones_pago(uuid,uuid) from authenticated;
revoke execute on function public.rpc_obtener_configuracion_institucion(uuid) from authenticated;
revoke execute on function public.rpc_obtener_contexto_implementacion() from authenticated;
revoke execute on function public.rpc_obtener_pago(uuid,uuid) from authenticated;
revoke execute on function public.rpc_obtener_plan_pago(uuid,uuid) from authenticated;
revoke execute on function public.rpc_obtener_seguridad_acceso(uuid) from authenticated;
revoke execute on function public.rpc_pago_aplicaciones_responsable(uuid,uuid) from authenticated;
revoke execute on function public.rpc_pagos_responsable(uuid,uuid) from authenticated;
revoke execute on function public.rpc_reactivar_alumno(uuid) from authenticated;
revoke execute on function public.rpc_reactivar_ciclo_escolar(uuid) from authenticated;
revoke execute on function public.rpc_reactivar_concepto_financiero(uuid,uuid) from authenticated;
revoke execute on function public.rpc_reactivar_grado(uuid,uuid) from authenticated;
revoke execute on function public.rpc_reactivar_jornada(uuid,uuid) from authenticated;
revoke execute on function public.rpc_reactivar_periodo_matricula(uuid) from authenticated;
revoke execute on function public.rpc_reactivar_plan_pago(uuid,uuid) from authenticated;
revoke execute on function public.rpc_reactivar_responsable(uuid) from authenticated;
revoke execute on function public.rpc_reactivar_seccion(uuid,uuid) from authenticated;
revoke execute on function public.rpc_reactivar_vinculo_responsable(uuid) from authenticated;
revoke execute on function public.rpc_reemplazar_permisos_rol_institucional(uuid,text[]) from authenticated;
revoke execute on function public.rpc_registrar_pago(uuid,jsonb,numeric,uuid,uuid,text,text,timestamp with time zone) from authenticated;
revoke execute on function public.rpc_resumen_financiero_alumno(uuid,uuid) from authenticated;
revoke execute on function public.rpc_resumen_financiero_responsable(uuid,uuid) from authenticated;
revoke execute on function public.rpc_vincular_alumno_responsable(uuid,uuid,text,boolean,boolean) from authenticated;

insert into public.schema_migrations(version,nombre,checksum)
values ('046','hardening_security_definer_api_only',null)
on conflict (version) do nothing;

commit;

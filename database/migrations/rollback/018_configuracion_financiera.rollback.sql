-- Rollback Migracion 018: configuracion financiera (conceptos + planes de pago).
-- Elimina las funciones, tablas, permisos y el registro de migracion.

begin;

-- Quitar permisos (los roles_permisos se limpian en cascada por FK).
delete from public.permisos
where codigo like 'configuracion.conceptos_financieros.%'
   or codigo like 'configuracion.planes_pago.%';

-- Eliminar funciones RPC (ciclo de vida via drop function).
drop function if exists public.rpc_listar_conceptos_financieros(uuid, boolean);
drop function if exists public.rpc_crear_concepto_financiero(text, numeric, text, uuid);
drop function if exists public.rpc_actualizar_concepto_financiero(uuid, text, numeric, text, uuid);
drop function if exists public.rpc_desactivar_concepto_financiero(uuid, text, uuid);
drop function if exists public.rpc_reactivar_concepto_financiero(uuid, uuid);
drop function if exists public.rpc_listar_planes_pago(uuid, boolean);
drop function if exists public.rpc_obtener_plan_pago(uuid, uuid);
drop function if exists public.rpc_crear_plan_pago(text, text, jsonb, uuid);
drop function if exists public.rpc_actualizar_plan_pago(uuid, text, text, jsonb, uuid);
drop function if exists public.rpc_desactivar_plan_pago(uuid, text, uuid);
drop function if exists public.rpc_reactivar_plan_pago(uuid, uuid);

-- Eliminar trigger y funcion de validacion de pertenencia.
drop trigger if exists trg_plan_cuotas_concepto_institucion_before on public.plan_cuotas;
drop function if exists public.trg_plan_cuotas_concepto_institucion();

-- Eliminar tablas (plan_cuotas primero por la FK).
drop table if exists public.plan_cuotas;
drop table if exists public.planes_pago;
drop table if exists public.conceptos_financieros;

-- Quitar registro de migracion.
delete from public.schema_migrations where version = '018';

commit;
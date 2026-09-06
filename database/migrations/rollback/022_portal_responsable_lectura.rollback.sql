-- Rollback Migracion 022: revierte el portal responsable (Bloque 022).
-- La migracion 022 es puramente ADITIVA de solo lectura: crea el helper de
-- puerta de seguridad usuario_es_responsable_financiero_del_alumno y las RPC
-- de lectura del portal responsable. No altera tablas, columnas ni checksums
-- de 001-021, por lo que el rollback solo elimina esas funciones.
begin;

drop function if exists public.usuario_es_responsable_financiero_del_alumno(uuid);
drop function if exists public.rpc_mis_alumnos_responsable();
drop function if exists public.rpc_resumen_financiero_responsable(uuid, uuid);
drop function if exists public.rpc_cargos_responsable(uuid, uuid);
drop function if exists public.rpc_pagos_responsable(uuid, uuid);
drop function if exists public.rpc_pago_aplicaciones_responsable(uuid, uuid);

delete from public.schema_migrations where version = '022';

commit;

-- Rollback Migracion 021: revierte Pagos / Cobranza.
-- Elimina: tablas pagos/pagos_aplicaciones, secuencia de recibo, triggers y
-- funciones internas, permisos academico.pagos.*, el estado ampliado de
-- cargos.estado (restaura a ('pendiente','anulado')) y las RPC de pagos.
-- Restaura las RPC de cargos 019 originales (listar/resumen con saldo pendiente
-- = monto_original de cargos 'pendiente', sin nocion de pagos parciales).
begin;

-- ======================================================================
-- 1. Eliminar objetos de 021
-- ======================================================================
drop table if exists public.pagos_aplicaciones;
drop table if exists public.pagos;

-- Triggers sobre tablas que permanecen (cargos) deben eliminarse antes que sus
-- funciones.
drop trigger if exists trg_cargos_no_anular_con_pagos_before on public.cargos;

drop function if exists public.rpc_registrar_pago(uuid, jsonb, numeric, uuid, uuid, text, text, timestamptz);
drop function if exists public.rpc_listar_pagos_alumno(uuid, uuid);
drop function if exists public.rpc_obtener_pago(uuid, uuid);
drop function if exists public.rpc_obtener_aplicaciones_pago(uuid, uuid);
drop function if exists public.rpc_anular_pago(uuid, text, uuid);
drop function if exists public.recalcular_estado_cargo(uuid);
drop function if exists public.trg_pagos_aplicaciones_guard();
drop function if exists public.trg_cargos_no_anular_con_pagos();
drop function if exists public.trg_pagos_aplicaciones_sync_estado();

drop sequence if exists public.seq_pagos_numero_recibo;

-- Permisos propios (se desvincula de roles antes de borrar).
delete from public.roles_permisos
where permiso_id in (select id from public.permisos where codigo like 'academico.pagos.%');
delete from public.permisos
where codigo in ('academico.pagos.ver','academico.pagos.registrar','academico.pagos.anular');

-- ======================================================================
-- 2. Restaurar cargos.estado a ('pendiente','anulado')
-- ======================================================================
alter table public.cargos drop constraint if exists ck_cargos_estado;
alter table public.cargos
  add constraint ck_cargos_estado check (estado in ('pendiente','anulado'));

-- ======================================================================
-- 3. Restaurar RPC de cargos 019 (listar matricula/alumno, resumen)
--    en su forma original (15 columnas; pendiente = monto_original).
-- ======================================================================
drop function if exists public.rpc_listar_cargos_matricula(uuid, uuid);
create or replace function public.rpc_listar_cargos_matricula(
  p_matricula_id uuid, p_institucion_id uuid default null)
returns table(id uuid, matricula_id uuid, alumno_id uuid, plan_pago_id uuid, orden integer,
              concepto_id uuid, concepto_nombre text, descripcion text, monto_original numeric,
              fecha_vencimiento date, estado text, fecha_generacion timestamptz,
              fecha_anulacion timestamptz, motivo_anulacion text, es_vencido boolean)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.cargos.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if not exists(select 1 from public.matriculas m
                where m.id = p_matricula_id and m.institucion_id = v) then
    raise exception 'La matricula no existe.' using errcode = 'P0002';
  end if;
  return query
    select c.id, c.matricula_id, c.alumno_id, c.plan_pago_id, c.orden,
           c.concepto_id, c.concepto_nombre, c.descripcion, c.monto_original,
           c.fecha_vencimiento, c.estado, c.fecha_generacion, c.fecha_anulacion,
           c.motivo_anulacion,
           (c.estado = 'pendiente' and c.fecha_vencimiento < current_date) as es_vencido
    from public.cargos c
    where c.matricula_id = p_matricula_id and c.institucion_id = v
    order by c.orden, c.fecha_vencimiento, c.id;
end $$;

drop function if exists public.rpc_listar_cargos_alumno(uuid, uuid);
create or replace function public.rpc_listar_cargos_alumno(
  p_alumno_id uuid, p_institucion_id uuid default null)
returns table(id uuid, matricula_id uuid, alumno_id uuid, plan_pago_id uuid, orden integer,
              concepto_id uuid, concepto_nombre text, descripcion text, monto_original numeric,
              fecha_vencimiento date, estado text, fecha_generacion timestamptz,
              fecha_anulacion timestamptz, motivo_anulacion text, es_vencido boolean)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.cargos.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if not exists(select 1 from public.alumnos a
                where a.id = p_alumno_id and a.institucion_id = v) then
    raise exception 'El alumno no existe.' using errcode = 'P0002';
  end if;
  return query
    select c.id, c.matricula_id, c.alumno_id, c.plan_pago_id, c.orden,
           c.concepto_id, c.concepto_nombre, c.descripcion, c.monto_original,
           c.fecha_vencimiento, c.estado, c.fecha_generacion, c.fecha_anulacion,
           c.motivo_anulacion,
           (c.estado = 'pendiente' and c.fecha_vencimiento < current_date) as es_vencido
    from public.cargos c
    where c.alumno_id = p_alumno_id and c.institucion_id = v
    order by c.fecha_vencimiento desc, c.orden, c.id;
end $$;

drop function if exists public.rpc_resumen_financiero_alumno(uuid, uuid);
create or replace function public.rpc_resumen_financiero_alumno(
  p_alumno_id uuid, p_institucion_id uuid default null)
returns table(alumno_id uuid, institucion_id uuid, total_obligaciones bigint,
              total_monto_original numeric, total_pendiente numeric, total_vencido numeric,
              total_anulado numeric)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.cargos.ver', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  if not exists(select 1 from public.alumnos a
                where a.id = p_alumno_id and a.institucion_id = v) then
    raise exception 'El alumno no existe.' using errcode = 'P0002';
  end if;
  return query
    select p_alumno_id as alumno_id, v as institucion_id,
           count(*) filter (where c.estado = 'pendiente')::bigint as total_obligaciones,
           coalesce(sum(c.monto_original) filter (where c.estado <> 'anulado'), 0) as total_monto_original,
           coalesce(sum(c.monto_original) filter (where c.estado = 'pendiente'), 0) as total_pendiente,
           coalesce(sum(c.monto_original) filter (where c.estado = 'pendiente'
                   and c.fecha_vencimiento < current_date), 0) as total_vencido,
           coalesce(sum(c.monto_original) filter (where c.estado = 'anulado'), 0) as total_anulado
    from public.cargos c
    where c.alumno_id = p_alumno_id and c.institucion_id = v;
end $$;

-- Re-assert grants como en 019 (las RPC restauradas vuelven a estar expuestas).
grant execute on function public.rpc_listar_cargos_matricula(uuid,uuid),
  public.rpc_listar_cargos_alumno(uuid,uuid),
  public.rpc_resumen_financiero_alumno(uuid,uuid) to authenticated;
grant execute on function public.rpc_listar_cargos_matricula(uuid,uuid),
  public.rpc_listar_cargos_alumno(uuid,uuid),
  public.rpc_resumen_financiero_alumno(uuid,uuid) to service_role;

delete from public.schema_migrations where version = '021';
commit;

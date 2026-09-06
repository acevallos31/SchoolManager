-- ======================================================================
-- Bloque 022 - Portal Responsable (prototipo de SOLO LECTURA).
-- ----------------------------------------------------------------------
-- Superficie de consulta real para el usuario autenticado con perfil de
-- padre/responsable: resolver sus alumnos vinculados y leer el estado
-- financiero (cargos/saldo/resumen/pagos/aplicaciones) de cada hijo.
--
-- REUTILIZA el modelo vigente (no lo duplica):
--   * 017 responsables (responsables + alumno_responsable);
--   * 021 pagos/cobranza (cargos, saldo derivado, pagos, aplicaciones).
--
-- Reglas de seguridad (aislamiento multi-institucion, sin fuga cross-tenant):
--   * NUNCA se acepta un responsable arbitrario del cliente: la identidad se
--     resuelve SIEMPRE desde el usuario autenticado (auth.uid()).
--   * Un responsable solo ve a un alumno si es responsable financiero activo
--     de ese alumno EN LA MISMA institucion (alumno_responsable.acceso_financiero=true).
--   * Los RPCs lanzan 'Acceso denegado.' (42501) de forma uniforme (tambien para
--     alumnos inexistentes) para NO revelar existencia.
--   * No escribe pagos/cargos ni toca tablas: solo lectura.
--
-- No reescribe 001-021. Siguiente numero libre tras 021.
-- ======================================================================
begin;

-- ----------------------------------------------------------------------
-- Precedencia explicita: exige que la 021 (pagos/cobranza) ya este aplicada
-- (pattern 017/021) y que exista la identidad de 009.
-- ----------------------------------------------------------------------
do $$ begin
  if not exists (select 1 from public.schema_migrations where version = '021') then
    raise exception 'Migracion 022 requiere la migracion 021 (pagos_cobranza).';
  end if;
  if to_regprocedure('auth.uid()') is null
     or public.usuario_actual_id() is null then
    raise notice 'identidad presente';
  end if;
exception
  when undefined_function then
    raise exception 'Migracion 022 requiere la migracion 009 (seguridad_rls_rpc).';
end $$;

-- ======================================================================
-- 1. Helper de puerta: es el usuario autenticado responsable financiero
--    activo del alumno (misma institucion)? SECURITY DEFINER, SOLO lo usan
--    los RPCs 022; no se expone a clientes.
-- ======================================================================
create or replace function public.usuario_es_responsable_financiero_del_alumno(
  p_alumno_id uuid
)
returns boolean
language sql stable security definer
set search_path = pg_catalog, public, pg_temp
as $$
  select exists (
    select 1
    from public.alumnos a
    join public.usuarios u
      on u.auth_user_id = auth.uid()
     and u.activo = true
     and u.persona_id is not null
    join public.responsables r
      on r.persona_id = u.persona_id
     and r.institucion_id = a.institucion_id
     and r.estado = 'activo'
    join public.alumno_responsable ar
      on ar.responsable_id = r.id
     and ar.alumno_id = a.id
     and ar.estado = 'activo'
     and ar.acceso_financiero = true
    where a.id = p_alumno_id
  );
$$;

-- ======================================================================
-- 2. RPC: MIS ALUMNOS (hijos con acceso financiero) del usuario autenticado.
--    Columnas (orden de lectura por indice):
--      0 id, 1 institucion_id, 2 nombres, 3 apellidos, 4 parentesco,
--      5 es_principal
-- ======================================================================
create or replace function public.rpc_mis_alumnos_responsable()
returns table(
  id uuid, institucion_id uuid, nombres text, apellidos text,
  parentesco text, es_principal boolean
)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  return query
    select a.id, a.institucion_id, p.nombres, p.apellidos,
           ar.parentesco, ar.es_principal
    from public.alumnos a
    join public.usuarios u
      on u.auth_user_id = auth.uid()
     and u.activo = true
     and u.persona_id is not null
    join public.responsables r
      on r.persona_id = u.persona_id
     and r.institucion_id = a.institucion_id
     and r.estado = 'activo'
    join public.alumno_responsable ar
      on ar.responsable_id = r.id
     and ar.alumno_id = a.id
     and ar.estado = 'activo'
     and ar.acceso_financiero = true
    join public.personas p on p.id = a.persona_id
    order by ar.es_principal desc, p.apellidos, p.nombres, a.id;
end $$;

-- ======================================================================
-- 3. RPC: RESUMEN FINANCIERO del hijo (proyeccion = rpc_resumen_financiero_alumno 021).
--    Guard: responsable financiero del alumno (helper), no permiso admin.
--    Columnas (indice): 0 alumno_id, 1 institucion_id, 2 total_obligaciones,
--      3 total_monto_original, 4 total_pendiente, 5 total_vencido,
--      6 total_anulado, 7 total_aplicado
-- ======================================================================
create or replace function public.rpc_resumen_financiero_responsable(
  p_alumno_id uuid, p_institucion_id uuid default null
)
returns table(alumno_id uuid, institucion_id uuid, total_obligaciones bigint,
              total_monto_original numeric, total_pendiente numeric,
              total_vencido numeric, total_anulado numeric, total_aplicado numeric)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if not public.usuario_es_responsable_financiero_del_alumno(p_alumno_id) then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  select a.institucion_id into v from public.alumnos a where a.id = p_alumno_id;
  if v is null then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  if p_institucion_id is not null and p_institucion_id <> v then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  return query
    with cargos_saldo as (
      select c.id, c.monto_original, c.fecha_vencimiento, c.estado,
             c.monto_original - coalesce(ap.aplicado, 0) as saldo,
             coalesce(ap.aplicado, 0) as aplicado
      from public.cargos c
      left join lateral (
        select sum(pa.monto_aplicado) as aplicado
        from public.pagos_aplicaciones pa
        join public.pagos pg on pg.id = pa.pago_id
        where pa.cargo_id = c.id and pa.estado = 'vigente' and pg.estado = 'registrado'
      ) ap on true
      where c.alumno_id = p_alumno_id and c.institucion_id = v
    )
    select p_alumno_id as alumno_id, v as institucion_id,
           count(*) filter (where estado in ('pendiente','parcial')
             and saldo > 0)::bigint as total_obligaciones,
           coalesce(sum(monto_original) filter (where estado <> 'anulado'), 0) as total_monto_original,
           coalesce(sum(saldo) filter (where estado in ('pendiente','parcial')
             and saldo > 0), 0) as total_pendiente,
           coalesce(sum(saldo) filter (where estado in ('pendiente','parcial')
             and saldo > 0 and fecha_vencimiento < current_date), 0) as total_vencido,
           coalesce(sum(monto_original) filter (where estado = 'anulado'), 0) as total_anulado,
           coalesce(sum(aplicado) filter (where estado <> 'anulado'), 0) as total_aplicado
    from cargos_saldo;
end $$;

-- ======================================================================
-- 4. RPC: CARGOS del hijo (proyeccion = rpc_listar_cargos_alumno 021).
--    Guard: responsable financiero.
--    Columnas (indice): 0 id, 1 matricula_id, 2 alumno_id, 3 plan_pago_id,
--      4 orden, 5 concepto_id, 6 concepto_nombre, 7 descripcion,
--      8 monto_original, 9 fecha_vencimiento, 10 estado, 11 fecha_generacion,
--      12 fecha_anulacion, 13 motivo_anulacion, 14 es_vencido, 15 saldo,
--      16 aplicado
-- ======================================================================
create or replace function public.rpc_cargos_responsable(
  p_alumno_id uuid, p_institucion_id uuid default null
)
returns table(id uuid, matricula_id uuid, alumno_id uuid, plan_pago_id uuid,
              orden integer, concepto_id uuid, concepto_nombre text,
              descripcion text, monto_original numeric, fecha_vencimiento date,
              estado text, fecha_generacion timestamptz,
              fecha_anulacion timestamptz, motivo_anulacion text,
              es_vencido boolean, saldo numeric, aplicado numeric)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if not public.usuario_es_responsable_financiero_del_alumno(p_alumno_id) then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  select a.institucion_id into v from public.alumnos a where a.id = p_alumno_id;
  if v is null then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  if p_institucion_id is not null and p_institucion_id <> v then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  return query
    select c.id, c.matricula_id, c.alumno_id, c.plan_pago_id, c.orden,
           c.concepto_id, c.concepto_nombre, c.descripcion, c.monto_original,
           c.fecha_vencimiento, c.estado, c.fecha_generacion, c.fecha_anulacion,
           c.motivo_anulacion,
           (c.estado in ('pendiente','parcial')
             and c.monto_original - coalesce(ap.aplicado, 0) > 0
             and c.fecha_vencimiento < current_date) as es_vencido,
           round(c.monto_original - coalesce(ap.aplicado, 0), 2) as saldo,
           coalesce(ap.aplicado, 0) as aplicado
    from public.cargos c
    left join lateral (
      select sum(pa.monto_aplicado) as aplicado
      from public.pagos_aplicaciones pa
      join public.pagos pg on pg.id = pa.pago_id
      where pa.cargo_id = c.id and pa.estado = 'vigente' and pg.estado = 'registrado'
    ) ap on true
    where c.alumno_id = p_alumno_id and c.institucion_id = v
    order by c.fecha_vencimiento desc, c.orden, c.id;
end $$;

-- ======================================================================
-- 5. RPC: PAGOS del hijo (proyeccion = rpc_listar_pagos_alumno 021).
--    Guard: responsable financiero.
--    Columnas (indice): 0 id, 1 institucion_id, 2 alumno_id, 3 responsable_id,
--      4 numero_recibo, 5 monto_total, 6 fecha_pago, 7 metodo_pago,
--      8 referencia_externa, 9 estado, 10 registrado_por, 11 fecha_anulacion,
--      12 anulado_por, 13 motivo_anulacion, 14 created_at
-- ======================================================================
create or replace function public.rpc_pagos_responsable(
  p_alumno_id uuid, p_institucion_id uuid default null
)
returns table(id uuid, institucion_id uuid, alumno_id uuid, responsable_id uuid,
              numero_recibo bigint, monto_total numeric, fecha_pago timestamptz,
              metodo_pago text, referencia_externa text, estado text,
              registrado_por uuid, fecha_anulacion timestamptz, anulado_por uuid,
              motivo_anulacion text, created_at timestamptz)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v uuid;
begin
  if not public.usuario_es_responsable_financiero_del_alumno(p_alumno_id) then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  select a.institucion_id into v from public.alumnos a where a.id = p_alumno_id;
  if v is null then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  if p_institucion_id is not null and p_institucion_id <> v then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  return query
    select pg.id, pg.institucion_id, pg.alumno_id, pg.responsable_id,
           pg.numero_recibo, pg.monto_total, pg.fecha_pago,
           pg.metodo_pago, pg.referencia_externa, pg.estado,
           pg.registrado_por, pg.fecha_anulacion, pg.anulado_por,
           pg.motivo_anulacion, pg.created_at
    from public.pagos pg
    where pg.alumno_id = p_alumno_id and pg.institucion_id = v
    order by pg.fecha_pago desc, pg.created_at desc, pg.id;
end $$;

-- ======================================================================
-- 6. RPC: APLICACIONES de un pago del hijo (proyeccion =
--    rpc_obtener_aplicaciones_pago 021). Guard: responsable financiero
--    del ALUMNO del pago (no acepta un pago ajeno).
--    Columnas (indice): 0 aplicacion_id, 1 pago_id, 2 cargo_id,
--      3 institucion_id, 4 monto_aplicado, 5 estado, 6 fecha_reversion,
--      7 cargo_estado, 8 concepto_nombre, 9 monto_original
-- ======================================================================
create or replace function public.rpc_pago_aplicaciones_responsable(
  p_pago_id uuid, p_institucion_id uuid default null
)
returns table(aplicacion_id uuid, pago_id uuid, cargo_id uuid,
              institucion_id uuid, monto_aplicado numeric, estado text,
              fecha_reversion timestamptz, cargo_estado text,
              concepto_nombre text, monto_original numeric)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v_alumno uuid;
        v uuid;
begin
  select pg.alumno_id, pg.institucion_id into v_alumno, v
  from public.pagos pg where pg.id = p_pago_id;
  if v_alumno is null then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  if not public.usuario_es_responsable_financiero_del_alumno(v_alumno) then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  if p_institucion_id is not null and p_institucion_id <> v then
    raise exception 'Acceso denegado.' using errcode = '42501';
  end if;
  return query
    select pa.id, pa.pago_id, pa.cargo_id, pa.institucion_id,
           pa.monto_aplicado, pa.estado, pa.fecha_reversion,
           c.estado, c.concepto_nombre, c.monto_original
    from public.pagos_aplicaciones pa
    join public.cargos c on c.id = pa.cargo_id
    where pa.pago_id = p_pago_id and pa.institucion_id = v
    order by pa.created_at, pa.id;
end $$;

-- ======================================================================
-- GRANTS Y REGISTRO (pattern 021): RPCs expuestos a authenticated y
-- service_role; helpers internos solo service_role; nada a public/anon.
-- ======================================================================
revoke execute on function
  public.usuario_es_responsable_financiero_del_alumno(uuid)
  from public, anon, authenticated;
grant execute on function
  public.usuario_es_responsable_financiero_del_alumno(uuid)
  to service_role;

revoke execute on function
  public.rpc_mis_alumnos_responsable(),
  public.rpc_resumen_financiero_responsable(uuid, uuid),
  public.rpc_cargos_responsable(uuid, uuid),
  public.rpc_pagos_responsable(uuid, uuid),
  public.rpc_pago_aplicaciones_responsable(uuid, uuid)
  from public, anon;

grant execute on function
  public.rpc_mis_alumnos_responsable(),
  public.rpc_resumen_financiero_responsable(uuid, uuid),
  public.rpc_cargos_responsable(uuid, uuid),
  public.rpc_pagos_responsable(uuid, uuid),
  public.rpc_pago_aplicaciones_responsable(uuid, uuid)
  to authenticated;

grant execute on function
  public.rpc_mis_alumnos_responsable(),
  public.rpc_resumen_financiero_responsable(uuid, uuid),
  public.rpc_cargos_responsable(uuid, uuid),
  public.rpc_pagos_responsable(uuid, uuid),
  public.rpc_pago_aplicaciones_responsable(uuid, uuid)
  to service_role;

insert into public.schema_migrations(version, nombre, checksum)
values ('022', 'portal_responsable_lectura', null)
on conflict (version) do nothing;

commit;

-- Cargos / Mensualidades / Cuentas por Cobrar (obligaciones generadas).
-- Bloque 020. Materializa las cuotas configuradas (planes_pago + plan_cuotas +
-- conceptos_financieros) en entidades de cargos por matricula/alumno. NUNCA se
-- modifican las tablas de configuracion para representar deuda generada: aqui se
-- leen conceptos/planes/cuotas como plantilla y se escriben obligaciones en
-- public.cargos. No se implementan pagos reales (bloque 021).
begin;
do $$ begin if not exists(select 1 from public.schema_migrations where version='018') then
  raise exception 'Migracion 019 requiere la migracion 018 (configuracion financiera).';
end if; end $$;

-- ============ PLAN ASIGNADO A UNA MATRICULA ============
-- La matricula conserva el plan de pago (plantilla) que se materializara.
alter table public.matriculas
  add column if not exists plan_pago_id uuid references public.planes_pago(id) on delete restrict;

-- Un plan asignado debe pertenecer a la misma institucion que la matricula.
create or replace function public.trg_matriculas_plan_institucion()
returns trigger language plpgsql set search_path = pg_catalog, public, pg_temp as $$
declare v_plan uuid;
begin
  if new.plan_pago_id is not null then
    select p.institucion_id into v_plan from public.planes_pago p where p.id = new.plan_pago_id;
    if v_plan is null or v_plan <> new.institucion_id then
      raise exception 'El plan de pago no pertenece a la institucion de la matricula.' using errcode = '23503';
    end if;
  end if;
  return new;
end $$;

create trigger trg_matriculas_plan_institucion_before
  before insert or update of plan_pago_id on public.matriculas
  for each row execute function public.trg_matriculas_plan_institucion();

revoke execute on function public.trg_matriculas_plan_institucion() from public, anon, authenticated;
grant execute on function public.trg_matriculas_plan_institucion() to service_role;

-- ============ TABLA DE CARGOS (OBLIGACIONES) ============
create table if not exists public.cargos (
    id uuid primary key default gen_random_uuid(),
    institucion_id uuid not null references public.instituciones(id) on delete restrict,
    matricula_id uuid not null references public.matriculas(id) on delete restrict,
    alumno_id uuid not null,
    plan_pago_id uuid not null references public.planes_pago(id) on delete restrict,
    concepto_id uuid references public.conceptos_financieros(id) on delete restrict,
    orden integer not null check (orden >= 0),
    concepto_nombre text not null,
    descripcion text,
    monto_original numeric(12,2) not null check (monto_original > 0),
    fecha_vencimiento date not null,
    estado text not null default 'pendiente' check (estado in ('pendiente','anulado')),
    fecha_generacion timestamptz not null default now(),
    fecha_anulacion timestamptz,
    motivo_anulacion text,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint fk_cargos_alumno_institucion foreign key (alumno_id, institucion_id)
      references public.alumnos(id, institucion_id) on delete restrict,
    constraint ck_cargos_anulacion_coherente check (
      (estado = 'anulado' and fecha_anulacion is not null
        and motivo_anulacion is not null and btrim(motivo_anulacion) <> '')
      or (estado <> 'anulado' and fecha_anulacion is null and motivo_anulacion is null)
    )
);

-- Indices de consulta (listado por matricula/alumno y resumen de saldo).
create index if not exists ix_cargos_matricula on public.cargos(matricula_id);
create index if not exists ix_cargos_alumno_estado
  on public.cargos(institucion_id, alumno_id, estado, fecha_vencimiento);
create index if not exists ix_cargos_concepto_id on public.cargos(concepto_id) where concepto_id is not null;

-- Integridad de dominio: el cargo debe ser coherente con su matricula
-- (misma institucion, mismo alumno, mismo plan asignado) y con el concepto
-- (misma institucion). Evita cruzar instituciones y desalinear el registro.
create or replace function public.trg_cargos_coherencia()
returns trigger language plpgsql set search_path = pg_catalog, public, pg_temp as $$
declare v_matricula public.matriculas%rowtype;
begin
  select * into v_matricula from public.matriculas m where m.id = new.matricula_id;
  if not found or v_matricula.institucion_id is distinct from new.institucion_id then
    raise exception 'La matricula no pertenece a la institucion del cargo.' using errcode = '23503';
  end if;
  if v_matricula.alumno_id is distinct from new.alumno_id then
    raise exception 'El alumno del cargo no coincide con el de la matricula.' using errcode = '23503';
  end if;
  if v_matricula.plan_pago_id is distinct from new.plan_pago_id then
    raise exception 'El plan del cargo no coincide con el plan asignado a la matricula.' using errcode = '23503';
  end if;
  if new.concepto_id is not null and not exists(
       select 1 from public.conceptos_financieros c
       where c.id = new.concepto_id and c.institucion_id = new.institucion_id) then
    raise exception 'El concepto no pertenece a la institucion del cargo.' using errcode = '23503';
  end if;
  return new;
end $$;

create trigger trg_cargos_coherencia_before
  before insert or update of matricula_id, institucion_id, alumno_id, plan_pago_id, concepto_id
  on public.cargos for each row execute function public.trg_cargos_coherencia();

revoke execute on function public.trg_cargos_coherencia() from public, anon, authenticated;
grant execute on function public.trg_cargos_coherencia() to service_role;

-- Superficie RPC-only: sin policies ni acceso directo para clientes Supabase.
alter table public.cargos enable row level security;
revoke all privileges on table public.cargos from public, anon, authenticated;
grant all privileges on table public.cargos to postgres, service_role;

-- ============ PERMISOS ============
insert into public.permisos(codigo,modulo,nombre) values
 ('academico.cargos.ver','academico','Ver cargos y mensualidades'),
 ('academico.cargos.generar','academico','Generar cargos desde un plan de pago'),
 ('academico.cargos.anular','academico','Anular cargos generados')
on conflict(codigo) do nothing;
insert into public.roles_permisos(rol_id,permiso_id)
select r.id,p.id from public.roles r cross join public.permisos p
 where r.codigo='admin' and (p.codigo like 'academico.cargos.%')
on conflict do nothing;

-- ============ RPC: LISTAR CARGOS POR MATRICULA ============
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

-- ============ RPC: LISTAR CARGOS POR ALUMNO ============
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

-- ============ RPC: RESUMEN FINANCIERO POR ALUMNO ============
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

-- ============ RPC: ASIGNAR PLAN DE PAGO A UNA MATRICULA ============
create or replace function public.rpc_asignar_plan_pago_matricula(
  p_matricula_id uuid, p_plan_pago_id uuid, p_institucion_id uuid default null)
returns uuid language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare m public.matriculas%rowtype; p public.planes_pago%rowtype; v uuid; x uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.cargos.generar', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  select * into m from public.matriculas where id = p_matricula_id and institucion_id = v for update;
  if not found then raise exception 'La matricula no existe.' using errcode = 'P0002'; end if;
  if m.estado in ('anulada','retirada','finalizada','trasladada') then
    raise exception 'No se puede asignar un plan a una matricula finalizada.' using errcode = '22023';
  end if;
  if exists(select 1 from public.cargos c where c.matricula_id = m.id and c.institucion_id = v)
     and m.plan_pago_id is distinct from p_plan_pago_id then
    raise exception 'No se puede cambiar el plan: ya se generaron cargos para la matricula.' using errcode = '23505';
  end if;
  select * into p from public.planes_pago where id = p_plan_pago_id and institucion_id = v;
  if not found then raise exception 'El plan de pago no existe.' using errcode = 'P0002'; end if;
  if p.activo = false then
    raise exception 'El plan de pago esta desactivado.' using errcode = '22023';
  end if;
  update public.matriculas set plan_pago_id = p.id, updated_at = now() where id = m.id;
  return m.id;
end $$;

-- ============ RPC: GENERAR CARGOS DESDE EL PLAN DE LA MATRICULA ============
-- Atomico: o se materializan todas las cuotas del plan o ninguna. Idempotente
-- por diseno: si la matricula ya tiene cargos, se rechaza la re-generacion.
create or replace function public.rpc_generar_cargos_matricula(
  p_matricula_id uuid, p_institucion_id uuid default null)
returns integer language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare m public.matriculas%rowtype; p public.planes_pago%rowtype; v uuid; v_inicio date; v_fin date;
        v_cargo_id uuid; v_count integer := 0; r record;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.cargos.generar', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  select * into m from public.matriculas where id = p_matricula_id and institucion_id = v for update;
  if not found then raise exception 'La matricula no existe.' using errcode = 'P0002'; end if;
  if m.estado not in ('pendiente','activa') then
    raise exception 'Solo se generan cargos para matriculas activas.' using errcode = '22023';
  end if;
  if m.plan_pago_id is null then
    raise exception 'La matricula no tiene un plan de pago asignado.' using errcode = 'P0002';
  end if;
  select * into p from public.planes_pago where id = m.plan_pago_id and institucion_id = v;
  if not found then raise exception 'El plan de pago no existe.' using errcode = 'P0002'; end if;
  if p.activo = false then
    raise exception 'El plan de pago esta desactivado.' using errcode = '22023';
  end if;

  -- Proteccion contra generacion duplicada.
  if exists(select 1 from public.cargos c where c.matricula_id = m.id and c.institucion_id = v) then
    raise exception 'Ya se generaron los cargos de este plan para la matricula.' using errcode = '23505';
  end if;

  -- Ancla temporal: el ciclo escolar de la matricula (debe estar configurado).
  select ce.fecha_inicio, ce.fecha_fin into v_inicio, v_fin
    from public.ciclos_escolares ce where ce.id = m.ciclo_id;
  if v_inicio is null or v_fin is null then
    raise exception 'El ciclo escolar de la matricula no tiene fechas configuradas.' using errcode = '22023';
  end if;

  -- Valida y materializa todas las cuotas del plan de forma atomica.
  for r in
    select q.orden, q.concepto_id, q.descripcion, q.monto, q.vencimiento_dias,
           coalesce(cf.nombre, nullif(btrim(coalesce(q.descripcion,'')),''),
                    p.nombre || ' - Cuota ' || (q.orden + 1)) as concepto_nombre
    from public.plan_cuotas q
    left join public.conceptos_financieros cf on cf.id = q.concepto_id
    where q.plan_id = p.id
    order by q.orden
  loop
    if r.monto is null or r.monto <= 0 then
      raise exception 'El monto de la cuota % no es valido.', r.orden using errcode = '22023';
    end if;
    if (v_inicio + r.vencimiento_dias) > v_fin then
      raise exception 'La cuota % vence fuera del ciclo escolar.', r.orden using errcode = '22023';
    end if;
    insert into public.cargos(institucion_id, matricula_id, alumno_id, plan_pago_id,
        concepto_id, orden, concepto_nombre, descripcion, monto_original, fecha_vencimiento, estado)
    values (v, m.id, m.alumno_id, p.id, r.concepto_id, r.orden, r.concepto_nombre,
        nullif(btrim(coalesce(r.descripcion,'')),''), r.monto, v_inicio + r.vencimiento_dias, 'pendiente')
    returning id into v_cargo_id;
    v_count := v_count + 1;
  end loop;

  if v_count = 0 then
    raise exception 'El plan no tiene cuotas para generar.' using errcode = '22023';
  end if;
  return v_count;
end $$;

-- ============ RPC: ANULAR UN CARGO (SOFT STATE) ============
create or replace function public.rpc_anular_cargo(
  p_cargo_id uuid, p_motivo text, p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare c public.cargos%rowtype; v uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  v := public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('academico.cargos.anular', v) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;
  select * into c from public.cargos where id = p_cargo_id and institucion_id = v for update;
  if not found then raise exception 'El cargo no existe.' using errcode = 'P0002'; end if;
  if c.estado = 'anulado' then
    raise exception 'El cargo ya esta anulado.' using errcode = '22023';
  end if;
  if btrim(coalesce(p_motivo, '')) = '' then
    raise exception 'El motivo de anulacion es obligatorio.' using errcode = '22023';
  end if;
  update public.cargos
  set estado = 'anulado', fecha_anulacion = now(), motivo_anulacion = btrim(p_motivo),
      updated_at = now()
  where id = c.id;
end $$;

-- ======================= GRANTS Y REGISTRO ================================
revoke execute on function public.rpc_listar_cargos_matricula(uuid,uuid),
  public.rpc_listar_cargos_alumno(uuid,uuid),
  public.rpc_resumen_financiero_alumno(uuid,uuid),
  public.rpc_asignar_plan_pago_matricula(uuid,uuid,uuid),
  public.rpc_generar_cargos_matricula(uuid,uuid),
  public.rpc_anular_cargo(uuid,text,uuid) from public, anon;
grant execute on function public.rpc_listar_cargos_matricula(uuid,uuid),
  public.rpc_listar_cargos_alumno(uuid,uuid),
  public.rpc_resumen_financiero_alumno(uuid,uuid),
  public.rpc_asignar_plan_pago_matricula(uuid,uuid,uuid),
  public.rpc_generar_cargos_matricula(uuid,uuid),
  public.rpc_anular_cargo(uuid,text,uuid) to authenticated;
grant execute on function public.rpc_listar_cargos_matricula(uuid,uuid),
  public.rpc_listar_cargos_alumno(uuid,uuid),
  public.rpc_resumen_financiero_alumno(uuid,uuid),
  public.rpc_asignar_plan_pago_matricula(uuid,uuid,uuid),
  public.rpc_generar_cargos_matricula(uuid,uuid),
  public.rpc_anular_cargo(uuid,text,uuid) to service_role;

insert into public.schema_migrations(version,nombre,checksum)
values('019','cargos_mensualidades_obligaciones',null) on conflict(version) do nothing;
commit;

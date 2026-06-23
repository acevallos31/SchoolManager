-- Funciones transaccionales de SchoolManager.
-- Ejecutar en Supabase SQL Editor despues de schoolmanager_erp_core.sql.

create extension if not exists pgcrypto;

begin;

create unique index if not exists ux_matriculas_alumno_ciclo
  on public.matriculas (alumno_id, ciclo_id);

alter table public.cargos add column if not exists numero_factura text;
alter table public.cargos add column if not exists fecha_emision date not null default current_date;
alter table public.cargos add column if not exists saldo numeric(10,2);
alter table public.cargos add column if not exists updated_at timestamptz;

create unique index if not exists ux_cargos_numero_factura
  on public.cargos (numero_factura)
  where numero_factura is not null;

create index if not exists ix_cargos_alumno_estado_vencimiento
  on public.cargos (alumno_id, estado, fecha_vencimiento);

create or replace function public.safe_due_date(
  p_year integer,
  p_month integer,
  p_day integer
)
returns date
language sql
immutable
as $$
  select make_date(
    p_year,
    greatest(1, least(12, p_month)),
    least(
      greatest(1, least(28, p_day)),
      extract(day from (
        date_trunc('month', make_date(p_year, greatest(1, least(12, p_month)), 1))
        + interval '1 month - 1 day'
      ))::integer
    )
  );
$$;

create or replace function public.next_factura_number(p_prefix text default 'FAC')
returns text
language plpgsql
volatile
as $$
declare
  v_number text;
begin
  v_number := p_prefix || '-' || to_char(now(), 'YYYYMMDDHH24MISS') || '-' || upper(substr(gen_random_uuid()::text, 1, 8));
  return v_number;
end;
$$;

create or replace function public.registrar_matricula_transaccional(
  p_alumno_id uuid,
  p_ciclo_id uuid,
  p_grado_id uuid,
  p_seccion_id uuid,
  p_plan_pago_id uuid,
  p_monto_matricula numeric default null,
  p_registrado_por uuid default null
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_ciclo public.ciclos_escolares%rowtype;
  v_plan public.planes_pago%rowtype;
  v_matricula public.matriculas%rowtype;
  v_factura public.cargos%rowtype;
  v_facturas jsonb := '[]'::jsonb;
  v_today date := current_date;
  v_monto_matricula numeric(10,2);
  v_cuotas integer;
  v_total_financiado numeric(10,2);
  v_monto_cuota numeric(10,2);
  v_base_date date;
  v_vencimiento date;
  v_estado text;
  v_cuota integer;
begin
  if p_alumno_id is null or p_ciclo_id is null or p_grado_id is null or p_seccion_id is null or p_plan_pago_id is null then
    raise exception 'Alumno, ciclo, grado, seccion y plan de pago son obligatorios.'
      using errcode = 'P0001';
  end if;

  select * into v_ciclo
  from public.ciclos_escolares
  where id = p_ciclo_id;

  if not found then
    raise exception 'El ciclo escolar seleccionado no existe.'
      using errcode = 'P0001';
  end if;

  if v_ciclo.matricula_inicio is not null
     and v_ciclo.matricula_fin is not null
     and (v_today < v_ciclo.matricula_inicio or v_today > v_ciclo.matricula_fin) then
    raise exception 'El periodo de matricula para % esta cerrado. Vigente del % al %.',
      v_ciclo.nombre,
      v_ciclo.matricula_inicio,
      v_ciclo.matricula_fin
      using errcode = 'P0001';
  end if;

  select * into v_plan
  from public.planes_pago
  where id = p_plan_pago_id
    and activo = true;

  if not found then
    raise exception 'El plan de pago seleccionado no existe o esta inactivo.'
      using errcode = 'P0001';
  end if;

  if exists (
    select 1
    from public.matriculas
    where alumno_id = p_alumno_id
      and ciclo_id = p_ciclo_id
  ) then
    raise exception 'Este alumno ya tiene una matricula registrada para el ciclo seleccionado.'
      using errcode = '23505';
  end if;

  v_monto_matricula := coalesce(nullif(p_monto_matricula, 0), v_plan.monto_matricula);

  insert into public.matriculas (
    alumno_id,
    ciclo_id,
    grado_id,
    seccion_id,
    plan_pago_id,
    fecha_matricula,
    monto,
    estado,
    registrado_por,
    created_at,
    updated_at
  )
  values (
    p_alumno_id,
    p_ciclo_id,
    p_grado_id,
    p_seccion_id,
    p_plan_pago_id,
    v_today,
    v_monto_matricula,
    'pendiente',
    p_registrado_por,
    now(),
    now()
  )
  returning * into v_matricula;

  if v_monto_matricula > 0 then
    insert into public.cargos (
      matricula_id,
      alumno_id,
      tipo,
      concepto,
      numero_cuota,
      monto,
      saldo,
      numero_factura,
      fecha_emision,
      fecha_vencimiento,
      estado,
      created_at,
      updated_at
    )
    values (
      v_matricula.id,
      p_alumno_id,
      'matricula',
      'Factura de matricula - ' || v_ciclo.nombre,
      null,
      v_monto_matricula,
      v_monto_matricula,
      public.next_factura_number('MAT'),
      v_today,
      coalesce(v_ciclo.matricula_fin, v_today),
      case when coalesce(v_ciclo.matricula_fin, v_today) < v_today then 'vencido' else 'pendiente' end,
      now(),
      now()
    )
    returning * into v_factura;

    v_facturas := v_facturas || to_jsonb(v_factura);
  end if;

  v_cuotas := greatest(1, coalesce(v_plan.cantidad_cuotas, 1));
  v_total_financiado := greatest(
    0,
    coalesce(v_plan.monto_total_anual, 0)
    - (coalesce(v_plan.monto_total_anual, 0) * (coalesce(v_plan.descuento_porcentaje, 0) / 100))
  );

  if v_total_financiado > 0 then
    v_monto_cuota := round(v_total_financiado / v_cuotas, 2);
    v_base_date := public.safe_due_date(
      extract(year from v_ciclo.fecha_inicio)::integer,
      coalesce(v_plan.mes_inicio, 1),
      coalesce(v_plan.dia_vencimiento, 10)
    );

    for v_cuota in 1..v_cuotas loop
      v_vencimiento := (v_base_date + ((v_cuota - 1) || ' months')::interval)::date;
      v_estado := case when v_vencimiento < v_today then 'vencido' else 'pendiente' end;

      insert into public.cargos (
        matricula_id,
        alumno_id,
        tipo,
        concepto,
        numero_cuota,
        monto,
        saldo,
        numero_factura,
        fecha_emision,
        fecha_vencimiento,
        estado,
        created_at,
        updated_at
      )
      values (
        v_matricula.id,
        p_alumno_id,
        case when v_cuotas = 1 then 'pago_anual' else 'mensualidad' end,
        case
          when v_cuotas = 1 then 'Factura anual - ' || v_plan.nombre
          else 'Factura ' || v_plan.nombre || ' - cuota ' || v_cuota || ' de ' || v_cuotas
        end,
        v_cuota,
        v_monto_cuota,
        v_monto_cuota,
        public.next_factura_number('FAC'),
        v_today,
        v_vencimiento,
        v_estado,
        now(),
        now()
      )
      returning * into v_factura;

      v_facturas := v_facturas || to_jsonb(v_factura);
    end loop;
  end if;

  return jsonb_build_object(
    'matricula', to_jsonb(v_matricula),
    'facturas', v_facturas,
    'mensaje', 'Matricula registrada. La factura de matricula y las facturas del plan fueron generadas correctamente.'
  );
exception
  when others then
    raise;
end;
$$;

commit;

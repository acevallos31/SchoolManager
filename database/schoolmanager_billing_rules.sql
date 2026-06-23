-- Reglas transaccionales para matricula, facturas/cargos y trazabilidad.
-- Ejecutar en Supabase SQL Editor antes de activar numeracion formal de facturas.

begin;

-- Un alumno no debe matricularse dos veces en el mismo ciclo.
create unique index if not exists ux_matriculas_alumno_ciclo
  on public.matriculas (alumno_id, ciclo_id);

-- La tabla cargos representa las facturas generadas automaticamente por matricula.
alter table public.cargos add column if not exists numero_factura text;
alter table public.cargos add column if not exists fecha_emision date not null default current_date;
alter table public.cargos add column if not exists saldo numeric(10,2);
alter table public.cargos add column if not exists updated_at timestamptz;

update public.cargos
set
  numero_factura = coalesce(numero_factura, 'FAC-' || to_char(created_at, 'YYYYMMDD') || '-' || left(id::text, 8)),
  saldo = coalesce(saldo, monto)
where numero_factura is null
   or saldo is null;

create unique index if not exists ux_cargos_numero_factura
  on public.cargos (numero_factura)
  where numero_factura is not null;

create index if not exists ix_cargos_alumno_estado_vencimiento
  on public.cargos (alumno_id, estado, fecha_vencimiento);

create index if not exists ix_pagos_cargo_fecha
  on public.pagos (cargo_id, fecha_pago);

-- Estados esperados para facturas/cargos.
do $$
begin
  if not exists (
    select 1 from pg_constraint where conname = 'ck_cargos_estado'
  ) then
    alter table public.cargos
      add constraint ck_cargos_estado check (estado in ('pendiente', 'vencido', 'pagado', 'condonado', 'anulado'));
  end if;
end $$;

commit;

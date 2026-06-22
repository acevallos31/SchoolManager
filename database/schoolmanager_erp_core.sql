create extension if not exists pgcrypto;

begin;

-- Roles oficiales del sistema.
alter table if exists public.usuarios drop constraint if exists usuarios_rol_check;
alter table if exists public.usuarios
  add constraint usuarios_rol_check check (rol in ('admin', 'operador', 'padre'));

create table if not exists public.jornadas (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz
);

create table if not exists public.niveles (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  orden integer not null default 0,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz
);

create table if not exists public.grados (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  activo boolean not null default true,
  created_at timestamptz not null default now()
);

alter table public.grados add column if not exists nivel_id uuid references public.niveles(id);
alter table public.grados add column if not exists orden integer not null default 0;
alter table public.grados add column if not exists updated_at timestamptz;

create table if not exists public.secciones (
  id uuid primary key default gen_random_uuid(),
  nombre text not null,
  activo boolean not null default true,
  created_at timestamptz not null default now()
);

alter table public.secciones add column if not exists grado_id uuid references public.grados(id);
alter table public.secciones add column if not exists jornada_id uuid references public.jornadas(id);
alter table public.secciones add column if not exists cupo integer;
alter table public.secciones add column if not exists updated_at timestamptz;
create unique index if not exists ux_secciones_grado_jornada_nombre
  on public.secciones (grado_id, jornada_id, lower(nombre))
  where grado_id is not null and jornada_id is not null;

alter table public.ciclos_escolares add column if not exists matricula_inicio date;
alter table public.ciclos_escolares add column if not exists matricula_fin date;
alter table public.ciclos_escolares add column if not exists updated_at timestamptz;

create table if not exists public.planes_pago (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  tipo text not null,
  descripcion text,
  monto_matricula numeric(10,2) not null default 0,
  monto_total_anual numeric(10,2) not null default 0,
  cantidad_cuotas integer not null default 10,
  mes_inicio integer not null default 1,
  dia_vencimiento integer not null default 10,
  descuento_porcentaje numeric(5,2) not null default 0,
  activo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz,
  constraint planes_pago_tipo_check check (tipo in ('10_meses', '12_meses', 'adelantado', '2_pagos', 'personalizado')),
  constraint planes_pago_cuotas_check check (cantidad_cuotas > 0),
  constraint planes_pago_mes_check check (mes_inicio between 1 and 12),
  constraint planes_pago_dia_check check (dia_vencimiento between 1 and 28),
  constraint planes_pago_montos_check check (monto_matricula >= 0 and monto_total_anual >= 0)
);

alter table public.matriculas add column if not exists grado_id uuid references public.grados(id);
alter table public.matriculas add column if not exists seccion_id uuid references public.secciones(id);
alter table public.matriculas add column if not exists plan_pago_id uuid references public.planes_pago(id);
alter table public.matriculas add column if not exists updated_at timestamptz;

create table if not exists public.cargos (
  id uuid primary key default gen_random_uuid(),
  matricula_id uuid not null references public.matriculas(id) on delete cascade,
  alumno_id uuid not null references public.alumnos(id) on delete cascade,
  tipo text not null,
  concepto text not null,
  numero_cuota integer,
  monto numeric(10,2) not null,
  fecha_vencimiento date not null,
  estado text not null default 'pendiente',
  created_at timestamptz not null default now(),
  updated_at timestamptz,
  constraint cargos_tipo_check check (tipo in ('matricula', 'mensualidad', 'pago_anual', 'cargo_extra')),
  constraint cargos_estado_check check (estado in ('pendiente', 'pagado', 'vencido', 'anulado', 'condonado')),
  constraint cargos_monto_check check (monto >= 0)
);

alter table public.pagos add column if not exists cargo_id uuid references public.cargos(id);
alter table public.pagos add column if not exists numero_recibo text unique;
alter table public.pagos add column if not exists anulado boolean not null default false;
alter table public.pagos add column if not exists updated_at timestamptz;

create index if not exists ix_grados_nivel on public.grados (nivel_id);
create index if not exists ix_secciones_grado on public.secciones (grado_id);
create index if not exists ix_secciones_jornada on public.secciones (jornada_id);
create index if not exists ix_matriculas_plan_pago on public.matriculas (plan_pago_id);
create index if not exists ix_cargos_alumno_estado on public.cargos (alumno_id, estado);
create index if not exists ix_cargos_matricula on public.cargos (matricula_id);
create index if not exists ix_cargos_vencimiento on public.cargos (fecha_vencimiento);
create index if not exists ix_pagos_cargo on public.pagos (cargo_id);

insert into public.jornadas (nombre)
values ('Matutina'), ('Vespertina'), ('Nocturna'), ('Sabatina')
on conflict (nombre) do nothing;

insert into public.niveles (nombre, orden)
values ('Preescolar', 1), ('Primaria', 2), ('Secundaria', 3)
on conflict (nombre) do nothing;

insert into public.planes_pago
  (nombre, tipo, descripcion, monto_matricula, monto_total_anual, cantidad_cuotas, mes_inicio, dia_vencimiento, descuento_porcentaje)
values
  ('Plan 10 Meses', '10_meses', 'Mensualidades de febrero a noviembre.', 1500, 12000, 10, 2, 10, 0),
  ('Plan 12 Meses', '12_meses', 'Mensualidades de enero a diciembre.', 1500, 14400, 12, 1, 10, 0),
  ('Pago Adelantado', 'adelantado', 'Pago anual en un solo cargo.', 1500, 12000, 1, 1, 10, 5),
  ('Plan 2 Pagos', '2_pagos', 'Dos pagos semestrales.', 1500, 12000, 2, 2, 10, 0)
on conflict (nombre) do nothing;

commit;

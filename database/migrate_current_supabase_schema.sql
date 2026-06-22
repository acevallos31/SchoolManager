create extension if not exists pgcrypto;

-- ============================================================
-- MIGRACION SCHOOLMANAGER
-- Estado actual detectado:
-- - alumnos(nombre, identidad, fecha_nacimiento, grado, seccion, tutor_id)
-- - matriculas(alumno_id, ciclo_id, fecha_matricula, monto, estado)
-- Objetivo:
-- - alumnos guarda solo datos personales
-- - grados/secciones/ciclos_escolares son catalogos
-- - matriculas relaciona alumno + ciclo + grado + seccion
-- ============================================================

begin;

-- =========================
-- 1. Catalogos academicos
-- =========================

create table if not exists public.grados (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  orden integer not null default 0,
  activo boolean not null default true
);

create table if not exists public.secciones (
  id uuid primary key default gen_random_uuid(),
  nombre text not null unique,
  activo boolean not null default true
);

alter table public.ciclos_escolares
  add column if not exists activo boolean not null default true;

insert into public.grados (nombre, orden)
values
  ('1er Grado', 1),
  ('2do Grado', 2),
  ('3er Grado', 3),
  ('4to Grado', 4),
  ('5to Grado', 5),
  ('6to Grado', 6),
  ('7mo Grado', 7),
  ('8vo Grado', 8),
  ('9no Grado', 9)
on conflict (nombre) do update
set orden = excluded.orden,
    activo = true;

insert into public.secciones (nombre)
select distinct trim(seccion)
from public.alumnos
where seccion is not null
  and trim(seccion) <> ''
on conflict (nombre) do nothing;

insert into public.secciones (nombre)
values ('A'), ('B'), ('C')
on conflict (nombre) do nothing;

-- Tambien migra cualquier grado historico que no este en el seed.
insert into public.grados (nombre, orden)
select distinct trim(grado), 999
from public.alumnos
where grado is not null
  and trim(grado) <> ''
on conflict (nombre) do nothing;

-- =========================
-- 2. Normalizar alumnos
-- =========================

alter table public.alumnos add column if not exists nombres text;
alter table public.alumnos add column if not exists apellidos text;
alter table public.alumnos add column if not exists sexo text;
alter table public.alumnos add column if not exists dni text;
alter table public.alumnos add column if not exists padres_encargados text;
alter table public.alumnos add column if not exists direccion text;
alter table public.alumnos add column if not exists updated_at timestamptz;

update public.alumnos
set
  nombres = coalesce(
    nullif(nombres, ''),
    nullif(split_part(nombre, ' ', 1), ''),
    'Sin nombre'
  ),
  apellidos = coalesce(
    nullif(apellidos, ''),
    nullif(trim(regexp_replace(coalesce(nombre, ''), '^\S+\s*', '')), ''),
    'Pendiente'
  ),
  dni = coalesce(nullif(dni, ''), identidad, id::text),
  sexo = coalesce(nullif(sexo, ''), 'O'),
  fecha_nacimiento = coalesce(fecha_nacimiento, date '2010-01-01'),
  padres_encargados = coalesce(nullif(padres_encargados, ''), 'Pendiente'),
  direccion = coalesce(nullif(direccion, ''), 'Pendiente'),
  estado = coalesce(nullif(estado, ''), 'activo');

alter table public.alumnos alter column nombres set not null;
alter table public.alumnos alter column apellidos set not null;
alter table public.alumnos alter column sexo set not null;
alter table public.alumnos alter column dni set not null;
alter table public.alumnos alter column fecha_nacimiento set not null;
alter table public.alumnos alter column padres_encargados set not null;
alter table public.alumnos alter column direccion set not null;

do $$
begin
  if not exists (
    select 1 from pg_constraint where conname = 'ck_alumnos_sexo'
  ) then
    alter table public.alumnos
    add constraint ck_alumnos_sexo check (sexo in ('M', 'F', 'O'));
  end if;

  if not exists (
    select 1 from pg_constraint where conname = 'uq_alumnos_dni'
  ) then
    alter table public.alumnos
    add constraint uq_alumnos_dni unique (dni);
  end if;
end $$;

create index if not exists ix_alumnos_estado on public.alumnos (estado);
create index if not exists ix_alumnos_dni on public.alumnos (dni);

-- Mantiene nombre/identidad/grado/seccion por ahora para no romper vistas antiguas.
-- Cuando confirmemos que la app ya usa nombres/apellidos/dni y matriculas.grado_id,
-- se pueden eliminar en una segunda migracion:
-- alter table public.alumnos drop column if exists nombre;
-- alter table public.alumnos drop column if exists identidad;
-- alter table public.alumnos drop column if exists grado;
-- alter table public.alumnos drop column if exists seccion;

-- =========================
-- 3. Normalizar matriculas
-- =========================

alter table public.matriculas add column if not exists grado_id uuid;
alter table public.matriculas add column if not exists seccion_id uuid;
alter table public.matriculas add column if not exists updated_at timestamptz;

update public.matriculas m
set grado_id = g.id
from public.alumnos a
join public.grados g on g.nombre = a.grado
where m.alumno_id = a.id
  and m.grado_id is null
  and a.grado is not null;

update public.matriculas m
set seccion_id = s.id
from public.alumnos a
join public.secciones s on s.nombre = a.seccion
where m.alumno_id = a.id
  and m.seccion_id is null
  and a.seccion is not null;

-- Si alguna matricula quedo sin grado/seccion porque el alumno no tenia dato,
-- asigna valores por defecto para poder trabajar desde la app.
update public.matriculas
set grado_id = (select id from public.grados order by orden, nombre limit 1)
where grado_id is null;

update public.matriculas
set seccion_id = (select id from public.secciones order by nombre limit 1)
where seccion_id is null;

alter table public.matriculas alter column grado_id set not null;
alter table public.matriculas alter column seccion_id set not null;

do $$
begin
  if not exists (
    select 1 from pg_constraint where conname = 'matriculas_grado_id_fkey'
  ) then
    alter table public.matriculas
    add constraint matriculas_grado_id_fkey
    foreign key (grado_id) references public.grados(id);
  end if;

  if not exists (
    select 1 from pg_constraint where conname = 'matriculas_seccion_id_fkey'
  ) then
    alter table public.matriculas
    add constraint matriculas_seccion_id_fkey
    foreign key (seccion_id) references public.secciones(id);
  end if;
end $$;

create index if not exists ix_matriculas_alumno on public.matriculas (alumno_id);
create index if not exists ix_matriculas_ciclo on public.matriculas (ciclo_id);
create index if not exists ix_matriculas_grado on public.matriculas (grado_id);
create index if not exists ix_matriculas_seccion on public.matriculas (seccion_id);

commit;


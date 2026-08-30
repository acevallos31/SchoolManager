-- Fase 1C - Bloque 2. Modelo academico historico y operaciones ACID.
-- Dependencias: 007_crear_rbac_base.
-- ciclo_id e institucion_id permanecen en matriculas como claves tecnicas de
-- integridad; grado_id se elimina porque se deriva completamente de seccion.

begin;

create extension if not exists pgcrypto;

alter table public.grados add column if not exists orden integer not null default 0;
alter table public.grados add column if not exists created_at timestamptz not null default now();
alter table public.grados add column if not exists updated_at timestamptz null;
alter table public.jornadas add column if not exists created_at timestamptz not null default now();
alter table public.jornadas add column if not exists updated_at timestamptz null;

alter table public.alumnos add column if not exists fecha_nacimiento date null;

-- Columnas legacy duplicadas: se conservan para compatibilidad de lectura, pero
-- dejan de ser obligatorias porque nombres/apellidos pertenecen a personas.
do $$
begin
  if exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'alumnos' and column_name = 'nombres'
  ) then
    alter table public.alumnos alter column nombres drop not null;
  end if;
  if exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'alumnos' and column_name = 'apellidos'
  ) then
    alter table public.alumnos alter column apellidos drop not null;
  end if;
  if exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'alumnos' and column_name = 'dni'
  ) then
    alter table public.alumnos alter column dni drop not null;
  end if;
end;
$$;

alter table public.secciones add column if not exists institucion_id uuid null;
alter table public.secciones add column if not exists ciclo_id uuid null;
alter table public.secciones add column if not exists cupo integer null;
alter table public.secciones add column if not exists created_at timestamptz not null default now();
alter table public.secciones add column if not exists updated_at timestamptz null;
alter table public.secciones add column if not exists fecha_desactivacion timestamptz null;
alter table public.secciones add column if not exists motivo_desactivacion text null;

do $$
begin
  if exists (select 1 from public.ciclos_escolares where institucion_id is null) then
    raise exception 'Migracion 008: existen ciclos sin institucion; no se inventara su contexto.';
  end if;

  if exists (select 1 from public.alumnos where institucion_id is null) then
    raise exception 'Migracion 008: existen alumnos sin institucion; complete el backfill antes de continuar.';
  end if;

  if exists (
    select 1
    from public.secciones s
    left join public.matriculas m on m.seccion_id = s.id
    group by s.id
    having count(distinct m.ciclo_id) <> 1
  ) then
    raise exception 'Migracion 008: una seccion sin matriculas o usada en varios ciclos no puede contextualizarse automaticamente.';
  end if;

  if exists (
    select 1 from public.matriculas where periodo_matricula_id is null
  ) then
    raise exception 'Migracion 008: existen matriculas sin periodo; complete el backfill antes de continuar.';
  end if;
end;
$$;

update public.secciones s
set ciclo_id = contexto.ciclo_id,
    institucion_id = contexto.institucion_id
from (
  select m.seccion_id, min(m.ciclo_id::text)::uuid as ciclo_id,
         min(c.institucion_id::text)::uuid as institucion_id
  from public.matriculas m
  join public.ciclos_escolares c on c.id = m.ciclo_id
  group by m.seccion_id
) contexto
where contexto.seccion_id = s.id
  and (s.ciclo_id is null or s.institucion_id is null);

do $$
begin
  if exists (
    select 1
    from public.matriculas m
    join public.secciones s on s.id = m.seccion_id
    join public.periodos_matricula pm on pm.id = m.periodo_matricula_id
    join public.alumnos a on a.id = m.alumno_id
    where s.ciclo_id <> m.ciclo_id
       or s.grado_id <> m.grado_id
       or pm.ciclo_id <> m.ciclo_id
       or a.institucion_id <> s.institucion_id
  ) then
    raise exception 'Migracion 008: matriculas incoherentes entre alumno, seccion, ciclo, grado o periodo.';
  end if;
end;
$$;

alter table public.ciclos_escolares alter column institucion_id set not null;
alter table public.alumnos alter column institucion_id set not null;
alter table public.secciones alter column institucion_id set not null;
alter table public.secciones alter column ciclo_id set not null;
alter table public.secciones alter column grado_id set not null;
alter table public.matriculas alter column periodo_matricula_id set not null;

alter table public.matriculas add column if not exists institucion_id uuid null;
update public.matriculas m
set institucion_id = s.institucion_id
from public.secciones s
where s.id = m.seccion_id and m.institucion_id is null;
alter table public.matriculas alter column institucion_id set not null;

alter table public.secciones drop constraint if exists secciones_grado_id_fkey;
alter table public.secciones drop constraint if exists secciones_jornada_id_fkey;
alter table public.matriculas drop constraint if exists matriculas_alumno_id_fkey;
alter table public.matriculas drop constraint if exists matriculas_ciclo_id_fkey;
alter table public.matriculas drop constraint if exists matriculas_grado_id_fkey;
alter table public.matriculas drop constraint if exists matriculas_seccion_id_fkey;
alter table public.matriculas drop constraint if exists matriculas_periodo_matricula_id_fkey;
alter table public.matriculas drop constraint if exists matriculas_registrado_por_fkey;
alter table public.matriculas drop constraint if exists uq_matriculas_alumno_ciclo;
alter table public.matriculas drop constraint if exists matriculas_alumno_id_ciclo_id_key;

drop index if exists public.ux_secciones_grado_jornada_nombre;
drop index if exists public.ux_secciones_grado_sin_jornada_nombre;
drop index if exists public.ix_matriculas_grado_id;

alter table public.matriculas drop column if exists grado_id;

do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'uq_ciclos_id_institucion') then
    alter table public.ciclos_escolares
      add constraint uq_ciclos_id_institucion unique (id, institucion_id);
  end if;
  if not exists (select 1 from pg_constraint where conname = 'uq_alumnos_id_institucion') then
    alter table public.alumnos
      add constraint uq_alumnos_id_institucion unique (id, institucion_id);
  end if;
  if not exists (select 1 from pg_constraint where conname = 'uq_secciones_id_ciclo_institucion') then
    alter table public.secciones
      add constraint uq_secciones_id_ciclo_institucion unique (id, ciclo_id, institucion_id);
  end if;
  if not exists (select 1 from pg_constraint where conname = 'uq_periodos_id_ciclo') then
    alter table public.periodos_matricula
      add constraint uq_periodos_id_ciclo unique (id, ciclo_id);
  end if;
end;
$$;

alter table public.secciones
  add constraint fk_secciones_ciclo_institucion foreign key (ciclo_id, institucion_id)
    references public.ciclos_escolares(id, institucion_id) on delete restrict,
  add constraint fk_secciones_grado foreign key (grado_id)
    references public.grados(id) on delete restrict,
  add constraint fk_secciones_jornada foreign key (jornada_id)
    references public.jornadas(id) on delete restrict;

alter table public.matriculas
  add constraint fk_matriculas_alumno_institucion foreign key (alumno_id, institucion_id)
    references public.alumnos(id, institucion_id) on delete restrict,
  add constraint fk_matriculas_seccion_contexto foreign key (seccion_id, ciclo_id, institucion_id)
    references public.secciones(id, ciclo_id, institucion_id) on delete restrict,
  add constraint fk_matriculas_periodo_ciclo foreign key (periodo_matricula_id, ciclo_id)
    references public.periodos_matricula(id, ciclo_id) on delete restrict,
  add constraint fk_matriculas_registrado_por foreign key (registrado_por)
    references public.usuarios(id) on delete restrict,
  add constraint uq_matriculas_alumno_ciclo unique (alumno_id, ciclo_id);

do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'ck_secciones_cupo') then
    alter table public.secciones add constraint ck_secciones_cupo
      check (cupo is null or cupo > 0);
  end if;
  if not exists (select 1 from pg_constraint where conname = 'ck_secciones_nombre_no_vacio') then
    alter table public.secciones add constraint ck_secciones_nombre_no_vacio
      check (btrim(nombre) <> '');
  end if;
  if not exists (select 1 from pg_constraint where conname = 'ck_matriculas_estado') then
    alter table public.matriculas add constraint ck_matriculas_estado check (
      estado in ('pendiente', 'activa', 'finalizada', 'retirada', 'anulada', 'trasladada')
    );
  end if;
end;
$$;

alter table public.matriculas drop constraint if exists ck_matriculas_anulacion_con_motivo;
alter table public.matriculas add constraint ck_matriculas_anulacion_coherente check (
  (estado = 'anulada' and fecha_anulacion is not null
    and motivo_anulacion is not null and btrim(motivo_anulacion) <> '')
  or (estado <> 'anulada' and fecha_anulacion is null and motivo_anulacion is null)
);

create unique index if not exists ux_secciones_contexto_jornada_nombre
  on public.secciones (institucion_id, ciclo_id, grado_id, jornada_id, lower(nombre))
  where jornada_id is not null;
create unique index if not exists ux_secciones_contexto_sin_jornada_nombre
  on public.secciones (institucion_id, ciclo_id, grado_id, lower(nombre))
  where jornada_id is null;
create index if not exists ix_secciones_ciclo_grado
  on public.secciones (ciclo_id, grado_id);
create index if not exists ix_secciones_institucion_activo
  on public.secciones (institucion_id, activo);
create index if not exists ix_matriculas_institucion_ciclo_estado
  on public.matriculas (institucion_id, ciclo_id, estado);

create table if not exists public.matricula_estado_historial (
  id uuid primary key default gen_random_uuid(),
  matricula_id uuid not null,
  estado_anterior text null,
  estado_nuevo text not null,
  fecha timestamptz not null default now(),
  usuario_id uuid null,
  motivo text null,
  constraint fk_matricula_historial_matricula foreign key (matricula_id)
    references public.matriculas(id) on delete restrict,
  constraint fk_matricula_historial_usuario foreign key (usuario_id)
    references public.usuarios(id) on delete restrict,
  constraint ck_matricula_historial_estado_anterior check (
    estado_anterior is null or estado_anterior in
      ('pendiente', 'activa', 'finalizada', 'retirada', 'anulada', 'trasladada')
  ),
  constraint ck_matricula_historial_estado_nuevo check (
    estado_nuevo in ('pendiente', 'activa', 'finalizada', 'retirada', 'anulada', 'trasladada')
  ),
  constraint ck_matricula_historial_motivo_no_vacio check (
    motivo is null or btrim(motivo) <> ''
  )
);
create index if not exists ix_matricula_historial_matricula_fecha
  on public.matricula_estado_historial (matricula_id, fecha);

create or replace function public.registrar_historial_estado_matricula()
returns trigger
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare
  v_usuario_id uuid := nullif(current_setting('schoolmanager.usuario_id', true), '')::uuid;
  v_motivo text := nullif(current_setting('schoolmanager.motivo_estado', true), '');
begin
  if tg_op = 'INSERT' or old.estado is distinct from new.estado then
    insert into public.matricula_estado_historial (
      matricula_id, estado_anterior, estado_nuevo, usuario_id, motivo
    ) values (
      new.id,
      case when tg_op = 'INSERT' then null else old.estado end,
      new.estado,
      coalesce(v_usuario_id, case when tg_op = 'INSERT' then new.registrado_por else null end),
      v_motivo
    );
  end if;

  perform set_config('schoolmanager.usuario_id', '', true);
  perform set_config('schoolmanager.motivo_estado', '', true);
  return new;
end;
$$;

drop trigger if exists trg_matriculas_historial_estado on public.matriculas;
create trigger trg_matriculas_historial_estado
after insert or update of estado on public.matriculas
for each row execute function public.registrar_historial_estado_matricula();

create or replace function public.crear_seccion(
  p_institucion_id uuid,
  p_ciclo_id uuid,
  p_grado_id uuid,
  p_jornada_id uuid,
  p_nombre text,
  p_cupo integer default null
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare v_id uuid;
begin
  if p_nombre is null or btrim(p_nombre) = '' then
    raise exception 'El nombre de la seccion es obligatorio.' using errcode = '22023';
  end if;
  if p_cupo is not null and p_cupo <= 0 then
    raise exception 'El cupo debe ser mayor que cero.' using errcode = '22023';
  end if;
  if not exists (select 1 from public.instituciones where id = p_institucion_id and activo) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;
  if not exists (
    select 1 from public.ciclos_escolares
    where id = p_ciclo_id and institucion_id = p_institucion_id and activo
  ) then
    raise exception 'El ciclo no pertenece a la institucion o esta inactivo.' using errcode = '23503';
  end if;
  if not exists (select 1 from public.grados where id = p_grado_id and activo) then
    raise exception 'El grado no existe o esta inactivo.' using errcode = '23503';
  end if;
  if p_jornada_id is not null and not exists (
    select 1 from public.jornadas where id = p_jornada_id and activo
  ) then
    raise exception 'La jornada no existe o esta inactiva.' using errcode = '23503';
  end if;

  insert into public.secciones (
    institucion_id, ciclo_id, grado_id, jornada_id, nombre, cupo
  ) values (
    p_institucion_id, p_ciclo_id, p_grado_id, p_jornada_id, btrim(p_nombre), p_cupo
  ) returning id into v_id;
  return v_id;
end;
$$;

create or replace function public.matricular_alumno(
  p_alumno_id uuid,
  p_seccion_id uuid,
  p_periodo_matricula_id uuid,
  p_registrado_por uuid default null
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare
  v_seccion public.secciones%rowtype;
  v_id uuid;
  v_ocupados integer;
begin
  select * into v_seccion
  from public.secciones
  where id = p_seccion_id
  for update;

  if not found or not v_seccion.activo then
    raise exception 'La seccion no existe o esta inactiva.' using errcode = '23503';
  end if;
  if not exists (
    select 1 from public.ciclos_escolares
    where id = v_seccion.ciclo_id
      and institucion_id = v_seccion.institucion_id
      and activo
  ) then
    raise exception 'El ciclo de la seccion esta inactivo o es incoherente.' using errcode = '23503';
  end if;
  if not exists (
    select 1 from public.alumnos
    where id = p_alumno_id
      and institucion_id = v_seccion.institucion_id
      and estado = 'activo'
  ) then
    raise exception 'El alumno no existe, esta inactivo o pertenece a otra institucion.' using errcode = '23503';
  end if;
  if not exists (
    select 1 from public.periodos_matricula
    where id = p_periodo_matricula_id
      and ciclo_id = v_seccion.ciclo_id
      and activo
  ) then
    raise exception 'El periodo no corresponde al ciclo o esta inactivo.' using errcode = '23503';
  end if;
  if p_registrado_por is not null and not exists (
    select 1 from public.usuarios where id = p_registrado_por and activo
  ) then
    raise exception 'El usuario registrador no existe o esta inactivo.' using errcode = '23503';
  end if;

  if v_seccion.cupo is not null then
    select count(*) into v_ocupados
    from public.matriculas
    where seccion_id = p_seccion_id and estado in ('pendiente', 'activa');
    if v_ocupados >= v_seccion.cupo then
      raise exception 'La seccion alcanzo su cupo.' using errcode = '23514';
    end if;
  end if;

  perform set_config('schoolmanager.usuario_id', coalesce(p_registrado_por::text, ''), true);
  perform set_config('schoolmanager.motivo_estado', 'Matricula creada', true);
  insert into public.matriculas (
    alumno_id, institucion_id, ciclo_id, seccion_id,
    periodo_matricula_id, registrado_por, estado
  ) values (
    p_alumno_id, v_seccion.institucion_id, v_seccion.ciclo_id, p_seccion_id,
    p_periodo_matricula_id, p_registrado_por, 'pendiente'
  ) returning id into v_id;
  return v_id;
end;
$$;

create or replace function public.cambiar_estado_matricula(
  p_matricula_id uuid,
  p_estado_nuevo text,
  p_usuario_id uuid,
  p_motivo text default null
)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare v_estado_actual text;
begin
  select estado into v_estado_actual
  from public.matriculas where id = p_matricula_id for update;
  if not found then
    raise exception 'La matricula no existe.' using errcode = 'P0002';
  end if;
  if not exists (select 1 from public.usuarios where id = p_usuario_id and activo) then
    raise exception 'El usuario no existe o esta inactivo.' using errcode = '23503';
  end if;
  if p_estado_nuevo = v_estado_actual then
    return;
  end if;
  if not (
    (v_estado_actual = 'pendiente' and p_estado_nuevo in ('activa', 'anulada'))
    or (v_estado_actual = 'activa' and p_estado_nuevo in
      ('finalizada', 'retirada', 'anulada', 'trasladada'))
  ) then
    raise exception 'Transicion de estado no permitida: % -> %', v_estado_actual, p_estado_nuevo
      using errcode = '22023';
  end if;
  if p_estado_nuevo in ('retirada', 'anulada', 'trasladada')
     and (p_motivo is null or btrim(p_motivo) = '') then
    raise exception 'El motivo es obligatorio para el estado solicitado.' using errcode = '22023';
  end if;

  perform set_config('schoolmanager.usuario_id', p_usuario_id::text, true);
  perform set_config('schoolmanager.motivo_estado', coalesce(btrim(p_motivo), ''), true);
  update public.matriculas
  set estado = p_estado_nuevo,
      fecha_anulacion = case when p_estado_nuevo = 'anulada' then now() else null end,
      motivo_anulacion = case when p_estado_nuevo = 'anulada' then btrim(p_motivo) else null end,
      updated_at = now()
  where id = p_matricula_id;
end;
$$;

create or replace function public.desactivar_alumno(
  p_alumno_id uuid,
  p_usuario_id uuid,
  p_motivo text
)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
begin
  if p_motivo is null or btrim(p_motivo) = '' then
    raise exception 'El motivo es obligatorio.' using errcode = '22023';
  end if;
  if not exists (select 1 from public.usuarios where id = p_usuario_id and activo) then
    raise exception 'El usuario no existe o esta inactivo.' using errcode = '23503';
  end if;
  perform 1 from public.alumnos where id = p_alumno_id for update;
  if not found then raise exception 'El alumno no existe.' using errcode = 'P0002'; end if;
  if exists (
    select 1 from public.matriculas
    where alumno_id = p_alumno_id and estado in ('pendiente', 'activa')
  ) then
    raise exception 'El alumno tiene una matricula vigente; procesela antes de desactivarlo.'
      using errcode = '23514';
  end if;
  update public.alumnos
  set estado = 'inactivo', fecha_desactivacion = now(),
      motivo_desactivacion = btrim(p_motivo), updated_at = now()
  where id = p_alumno_id;
end;
$$;

create or replace function public.reactivar_alumno(
  p_alumno_id uuid,
  p_usuario_id uuid
)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
begin
  if not exists (select 1 from public.usuarios where id = p_usuario_id and activo) then
    raise exception 'El usuario no existe o esta inactivo.' using errcode = '23503';
  end if;
  update public.alumnos
  set estado = 'activo', fecha_desactivacion = null,
      motivo_desactivacion = null, updated_at = now()
  where id = p_alumno_id;
  if not found then raise exception 'El alumno no existe.' using errcode = 'P0002'; end if;
end;
$$;

create or replace function public.desactivar_seccion(
  p_seccion_id uuid,
  p_motivo text
)
returns void
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
begin
  if p_motivo is null or btrim(p_motivo) = '' then
    raise exception 'El motivo es obligatorio.' using errcode = '22023';
  end if;
  perform 1 from public.secciones where id = p_seccion_id for update;
  if not found then raise exception 'La seccion no existe.' using errcode = 'P0002'; end if;
  if exists (
    select 1 from public.matriculas
    where seccion_id = p_seccion_id and estado in ('pendiente', 'activa')
  ) then
    raise exception 'La seccion tiene matriculas vigentes.' using errcode = '23514';
  end if;
  update public.secciones
  set activo = false, fecha_desactivacion = now(),
      motivo_desactivacion = btrim(p_motivo), updated_at = now()
  where id = p_seccion_id;
end;
$$;

create or replace function public.crear_alumno_nueva_persona(
  p_institucion_id uuid,
  p_nombres text,
  p_apellidos text,
  p_fecha_nacimiento date default null,
  p_rne text default null,
  p_codigo_interno text default null
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare v_persona_id uuid; v_alumno_id uuid;
begin
  if not exists (select 1 from public.instituciones where id = p_institucion_id and activo) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;
  insert into public.personas (nombres, apellidos)
  values (p_nombres, p_apellidos) returning id into v_persona_id;
  insert into public.alumnos (
    persona_id, institucion_id, fecha_nacimiento, rne, codigo_interno
  ) values (
    v_persona_id, p_institucion_id, p_fecha_nacimiento, p_rne, p_codigo_interno
  ) returning id into v_alumno_id;
  return v_alumno_id;
end;
$$;

create or replace function public.crear_alumno_para_persona(
  p_persona_id uuid,
  p_institucion_id uuid,
  p_fecha_nacimiento date default null,
  p_rne text default null,
  p_codigo_interno text default null
)
returns uuid
language plpgsql
security invoker
set search_path = pg_catalog, public
as $$
declare v_alumno_id uuid;
begin
  if not exists (select 1 from public.personas where id = p_persona_id and estado = 'activo') then
    raise exception 'La persona no existe o esta inactiva.' using errcode = '23503';
  end if;
  if not exists (select 1 from public.instituciones where id = p_institucion_id and activo) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;
  insert into public.alumnos (
    persona_id, institucion_id, fecha_nacimiento, rne, codigo_interno
  ) values (
    p_persona_id, p_institucion_id, p_fecha_nacimiento, p_rne, p_codigo_interno
  ) returning id into v_alumno_id;
  return v_alumno_id;
end;
$$;

insert into public.schema_migrations (version, nombre, checksum)
values ('008', 'normalizar_modelo_academico', null)
on conflict (version) do nothing;

commit;

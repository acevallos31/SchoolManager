-- Fase 1A. Objetivo: crear Persona global y referencias de transicion.
-- Dependencias: 002_crear_instituciones_y_configuracion_identificadores.
-- Las columnas nuevas son anulables para conservar compatibilidad legacy.

create extension if not exists pgcrypto;

create table if not exists public.personas (
  id uuid primary key default gen_random_uuid(),
  nombres text not null,
  apellidos text not null,
  tipo_identificacion text null,
  numero_identificacion text null,
  numero_identificacion_normalizado text null,
  pais_emisor text null,
  telefono text null,
  correo text null,
  direccion text null,
  estado text not null default 'activo',
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  fecha_desactivacion timestamptz null,
  motivo_desactivacion text null,
  constraint ck_personas_nombres_no_vacios check (btrim(nombres) <> ''),
  constraint ck_personas_apellidos_no_vacios check (btrim(apellidos) <> ''),
  constraint ck_personas_estado check (estado in ('activo', 'inactivo')),
  constraint ck_personas_identificacion_coherente check (
    (
      numero_identificacion is null
      and tipo_identificacion is null
      and numero_identificacion_normalizado is null
    )
    or (
      numero_identificacion is not null
      and btrim(numero_identificacion) <> ''
      and tipo_identificacion is not null
      and btrim(tipo_identificacion) <> ''
      and numero_identificacion_normalizado is not null
      and btrim(numero_identificacion_normalizado) <> ''
    )
  )
);

create unique index if not exists ux_personas_documento_normalizado
  on public.personas (
    lower(tipo_identificacion),
    lower(coalesce(pais_emisor, '')),
    numero_identificacion_normalizado
  )
  where numero_identificacion_normalizado is not null;

alter table public.alumnos add column if not exists persona_id uuid null
  references public.personas(id);
alter table public.alumnos add column if not exists institucion_id uuid null
  references public.instituciones(id);
alter table public.alumnos add column if not exists rne text null;
alter table public.alumnos add column if not exists codigo_interno text null;
alter table public.alumnos add column if not exists fecha_desactivacion timestamptz null;
alter table public.alumnos add column if not exists motivo_desactivacion text null;

alter table public.usuarios add column if not exists persona_id uuid null
  references public.personas(id);
alter table public.usuarios add column if not exists auth_user_id uuid null;
alter table public.usuarios add column if not exists fecha_desactivacion timestamptz null;
alter table public.usuarios add column if not exists motivo_desactivacion text null;

create unique index if not exists ux_alumnos_codigo_interno_por_institucion
  on public.alumnos (institucion_id, lower(codigo_interno))
  where institucion_id is not null and codigo_interno is not null;

-- El RNE es global y nullable. Su comparacion es exacta hasta definir formato oficial.
create unique index if not exists ux_alumnos_rne_global
  on public.alumnos (rne)
  where rne is not null;

create unique index if not exists ux_alumnos_persona_institucion
  on public.alumnos (persona_id, institucion_id)
  where persona_id is not null and institucion_id is not null;

create unique index if not exists ux_usuarios_auth_user_id
  on public.usuarios (auth_user_id)
  where auth_user_id is not null;

create unique index if not exists ux_usuarios_persona_id
  on public.usuarios (persona_id)
  where persona_id is not null;

create index if not exists ix_alumnos_persona_id on public.alumnos (persona_id);
create index if not exists ix_alumnos_institucion_id on public.alumnos (institucion_id);

comment on column public.personas.pais_emisor is
  'Nullable en Fase 1A; una configuracion futura podra exigirlo segun tipo documental.';

comment on column public.alumnos.rne is
  'Identificador educativo. La unicidad global definitiva requiere aprobacion posterior.';

-- Migracion 046 - base de sesiones para Demo aislada.
-- 049B: agrega clasificacion institucional y ciclo de vida de sandbox.
--
-- IMPORTANTE:
-- - Este esquema es compartido por los entornos, pero NO habilita la Demo.
-- - Produccion conserva todas sus instituciones existentes como 'normal'.
-- - El entorno Demo separado sera el unico que cree demo_template/demo_sandbox.
-- - No crea datos, usuarios Auth, endpoints ni permisos publicos.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '045') then
    raise exception 'La migracion 046 requiere la 045 aplicada previamente.';
  end if;
end
$$;

alter table public.instituciones
  add column if not exists tipo text not null default 'normal';

do $$
begin
  if not exists (
    select 1
    from pg_constraint
    where conname = 'ck_instituciones_tipo'
      and conrelid = 'public.instituciones'::regclass
  ) then
    alter table public.instituciones
      add constraint ck_instituciones_tipo
      check (tipo in ('normal', 'demo_template', 'demo_sandbox'));
  end if;
end
$$;

create table if not exists public.demo_sessions (
  id uuid primary key default gen_random_uuid(),
  auth_user_id uuid not null,
  institucion_id uuid not null
    references public.instituciones(id) on delete restrict,
  plantilla_institucion_id uuid not null
    references public.instituciones(id) on delete restrict,
  estado text not null default 'activa'
    check (estado in ('activa', 'expirada', 'reiniciada', 'cerrada')),
  created_at timestamptz not null default now(),
  last_activity_at timestamptz not null default now(),
  expires_at timestamptz not null default (now() + interval '2 hours'),
  max_expires_at timestamptz not null default (now() + interval '24 hours'),
  closed_at timestamptz null,
  constraint ck_demo_sessions_instituciones_distintas
    check (institucion_id <> plantilla_institucion_id),
  constraint ck_demo_sessions_expiracion
    check (
      expires_at > created_at
      and max_expires_at >= expires_at
      and last_activity_at >= created_at
    ),
  constraint ck_demo_sessions_cierre
    check (
      (estado = 'activa' and closed_at is null)
      or
      (estado <> 'activa' and closed_at is not null and closed_at >= created_at)
    )
);

create unique index if not exists ux_demo_sessions_auth_activa
  on public.demo_sessions(auth_user_id)
  where estado = 'activa';

create unique index if not exists ux_demo_sessions_institucion
  on public.demo_sessions(institucion_id);

create index if not exists ix_demo_sessions_estado_expira
  on public.demo_sessions(estado, expires_at);

create or replace function public.validar_demo_session_tipos()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
  if not exists (
    select 1
    from public.instituciones i
    where i.id = new.institucion_id
      and i.tipo = 'demo_sandbox'
      and i.activo
  ) then
    raise exception 'La institucion de una sesion Demo debe ser una demo_sandbox activa.'
      using errcode = '23514';
  end if;

  if not exists (
    select 1
    from public.instituciones i
    where i.id = new.plantilla_institucion_id
      and i.tipo = 'demo_template'
      and i.activo
  ) then
    raise exception 'La plantilla de una sesion Demo debe ser una demo_template activa.'
      using errcode = '23514';
  end if;

  return new;
end
$$;

revoke all on function public.validar_demo_session_tipos()
  from public, anon, authenticated;

drop trigger if exists trg_demo_sessions_validar_tipos on public.demo_sessions;
create trigger trg_demo_sessions_validar_tipos
  before insert or update of institucion_id, plantilla_institucion_id
  on public.demo_sessions
  for each row
  execute function public.validar_demo_session_tipos();

create or replace function public.proteger_tipo_institucion_demo()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
  if new.tipo is distinct from old.tipo then
    if exists (
      select 1
      from public.demo_sessions ds
      where ds.institucion_id = old.id
         or ds.plantilla_institucion_id = old.id
    ) then
      raise exception 'No se puede cambiar el tipo de una institucion referenciada por sesiones Demo.'
        using errcode = '23514';
    end if;
  end if;

  if old.activo and not new.activo and exists (
    select 1
    from public.demo_sessions ds
    where ds.estado = 'activa'
      and (ds.institucion_id = old.id or ds.plantilla_institucion_id = old.id)
  ) then
    raise exception 'No se puede desactivar una institucion con sesiones Demo activas.'
      using errcode = '23514';
  end if;

  return new;
end
$$;

revoke all on function public.proteger_tipo_institucion_demo()
  from public, anon, authenticated;

drop trigger if exists trg_instituciones_proteger_tipo_demo on public.instituciones;
create trigger trg_instituciones_proteger_tipo_demo
  before update of tipo, activo
  on public.instituciones
  for each row
  execute function public.proteger_tipo_institucion_demo();

alter table public.demo_sessions enable row level security;

revoke all on table public.demo_sessions
  from public, anon, authenticated;

insert into public.schema_migrations(version, nombre, checksum)
values ('046', 'demo_sandbox_sesiones', null)
on conflict (version) do nothing;

commit;

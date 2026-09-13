-- Migracion 029 - operaciones seguras de roles institucionales.
begin;

do $$ begin
  if not exists (select 1 from public.schema_migrations where version='028') then
    raise exception 'La migracion 029 requiere la 028 aplicada previamente.';
  end if;
end $$;

create table if not exists public.seguridad_auditoria (
  id uuid primary key default gen_random_uuid(),
  actor_usuario_id uuid null references public.usuarios(id) on delete set null,
  institucion_id uuid null references public.instituciones(id) on delete set null,
  accion text not null check (btrim(accion)<>''),
  entidad_tipo text not null check (btrim(entidad_tipo)<>''),
  entidad_id uuid null,
  detalle jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now()
);
create index if not exists ix_seguridad_auditoria_institucion_fecha on public.seguridad_auditoria(institucion_id,created_at desc);
alter table public.seguridad_auditoria enable row level security;

insert into public.schema_migrations(version,nombre,checksum)
values('029','operaciones_roles_institucionales',null)
on conflict(version) do nothing;
commit;

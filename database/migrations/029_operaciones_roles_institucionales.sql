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
revoke all on table public.seguridad_auditoria from public,anon,authenticated;
grant select,insert on table public.seguridad_auditoria to service_role;

create or replace function public.usuario_es_admin_institucional(p_usuario_id uuid,p_institucion_id uuid)
returns boolean language sql stable security definer set search_path=pg_catalog,public,pg_temp as $$
  select exists(
    select 1 from public.usuarios u
    join public.usuarios_roles ur on ur.usuario_id=u.id and ur.institucion_id=p_institucion_id and ur.activo
    join public.roles r on r.id=ur.rol_id and r.activo
    join public.roles_permisos rp on rp.rol_id=r.id
    join public.permisos p on p.id=rp.permiso_id
    where u.id=p_usuario_id and u.activo and p.codigo='identidad.roles.editar' and p.estado='vigente'
  ) and exists(
    select 1 from public.usuarios u
    join public.usuarios_roles ur on ur.usuario_id=u.id and ur.institucion_id=p_institucion_id and ur.activo
    join public.roles r on r.id=ur.rol_id and r.activo
    join public.roles_permisos rp on rp.rol_id=r.id
    join public.permisos p on p.id=rp.permiso_id
    where u.id=p_usuario_id and u.activo and p.codigo='identidad.usuarios.asignar_roles' and p.estado='vigente'
  );
$$;
revoke all on function public.usuario_es_admin_institucional(uuid,uuid) from public,anon,authenticated;
grant execute on function public.usuario_es_admin_institucional(uuid,uuid) to service_role;

create or replace function public.rpc_crear_rol_institucional(
  p_institucion_id uuid,p_codigo text,p_nombre text,p_descripcion text default null
)
returns uuid language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare v_id uuid; v_codigo text:=lower(btrim(coalesce(p_codigo,'')));
begin
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('identidad.roles.crear',p_institucion_id) then
    raise exception 'Permiso denegado.' using errcode='42501';
  end if;
  if not exists(select 1 from public.instituciones where id=p_institucion_id and activo) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode='23503';
  end if;
  if v_codigo='' or btrim(coalesce(p_nombre,''))='' then
    raise exception 'Codigo y nombre son obligatorios.' using errcode='22023';
  end if;
  if exists(select 1 from public.roles where institucion_id is null and tipo='plataforma' and codigo=v_codigo) then
    raise exception 'Codigo reservado para plataforma.' using errcode='23514';
  end if;
  insert into public.roles(codigo,nombre,descripcion,es_sistema,activo,institucion_id,tipo,protegido)
  values(v_codigo,btrim(p_nombre),nullif(btrim(coalesce(p_descripcion,'')),''),false,true,p_institucion_id,'institucional',false)
  returning id into v_id;
  insert into public.seguridad_auditoria(actor_usuario_id,institucion_id,accion,entidad_tipo,entidad_id,detalle)
  values(public.usuario_actual_id(),p_institucion_id,'rol_institucional.crear','rol',v_id,jsonb_build_object('codigo',v_codigo));
  return v_id;
end $$;

revoke execute on function public.rpc_crear_rol_institucional(uuid,text,text,text) from public,anon;
grant execute on function public.rpc_crear_rol_institucional(uuid,text,text,text) to authenticated,service_role;

insert into public.schema_migrations(version,nombre,checksum)
values('029','operaciones_roles_institucionales',null)
on conflict(version) do nothing;
commit;

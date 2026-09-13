-- Migracion 036 - autoridad institucional estricta para administracion RBAC.
--
-- La funcion historica usuario_tiene_permiso_actual conserva el fallback de
-- asignaciones globales por compatibilidad con modulos legacy. Ese fallback no
-- debe convertir a un admin global legacy en administrador de todas las
-- instituciones. Esta migracion agrega una frontera estricta en tablas RBAC:
-- asignacion activa en la institucion exacta, o platform_admin global.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '035') then
    raise exception 'La migracion 036 requiere la 035 aplicada previamente.';
  end if;
end
$$;

create or replace function public.usuario_tiene_permiso_institucional_estricto(
  p_permiso_codigo text,
  p_institucion_id uuid
)
returns boolean
language sql
stable
security definer
set search_path = pg_catalog, public, pg_temp
as $$
  select p_institucion_id is not null and exists (
    select 1
    from public.usuarios u
    join public.usuarios_roles ur
      on ur.usuario_id = u.id
     and ur.activo
    join public.roles r
      on r.id = ur.rol_id
     and r.activo
    join public.roles_permisos rp on rp.rol_id = r.id
    join public.permisos p
      on p.id = rp.permiso_id
     and p.estado = 'vigente'
     and p.ambito = 'institucion'
    where u.auth_user_id = auth.uid()
      and u.activo
      and p.codigo = p_permiso_codigo
      and (
        ur.institucion_id = p_institucion_id
        or (
          ur.institucion_id is null
          and r.tipo = 'plataforma'
          and r.codigo = 'platform_admin'
        )
      )
  );
$$;

revoke all on function public.usuario_tiene_permiso_institucional_estricto(text, uuid)
  from public, anon, authenticated;
grant execute on function public.usuario_tiene_permiso_institucional_estricto(text, uuid)
  to service_role;

-- ----------------------------------------------------------------------
-- Definiciones de roles institucionales.
-- ----------------------------------------------------------------------
create or replace function public.trg_roles_autoridad_institucional_estricta()
returns trigger
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_institucion_id uuid;
begin
  -- Escrituras privilegiadas sin identidad de usuario (migraciones, service
  -- operations y fixtures DB) mantienen su contrato administrativo.
  if auth.uid() is null then
    return new;
  end if;

  if tg_op = 'INSERT' then
    if new.tipo = 'institucional'
       and not public.usuario_tiene_permiso_institucional_estricto(
         'identidad.roles.crear', new.institucion_id
       ) then
      raise exception 'Se requiere autoridad explicita en la institucion para crear roles.'
        using errcode = '42501';
    end if;
    return new;
  end if;

  if old.tipo = 'institucional' or new.tipo = 'institucional' then
    if old.tipo is distinct from new.tipo
       or old.institucion_id is distinct from new.institucion_id then
      raise exception 'El tipo y la institucion de un rol institucional son inmutables.'
        using errcode = '23514';
    end if;

    v_institucion_id := old.institucion_id;
    if not public.usuario_tiene_permiso_institucional_estricto(
      'identidad.roles.editar', v_institucion_id
    ) then
      raise exception 'Se requiere autoridad explicita en la institucion para editar roles.'
        using errcode = '42501';
    end if;

    -- Marca transaccional para permitir que rpc_desactivar_rol_institucional
    -- historice sus asignaciones despues de haber desactivado el rol.
    if old.activo and not new.activo then
      perform set_config(
        'schoolmanager.rbac_rol_desactivado', old.id::text, true
      );
    end if;
  end if;

  return new;
end
$$;

revoke all on function public.trg_roles_autoridad_institucional_estricta()
  from public, anon, authenticated;
grant execute on function public.trg_roles_autoridad_institucional_estricta()
  to service_role;

drop trigger if exists trg_roles_autoridad_institucional_estricta_before
  on public.roles;
create trigger trg_roles_autoridad_institucional_estricta_before
  before insert or update of codigo, nombre, descripcion, activo,
    institucion_id, tipo, rol_base_id, protegido, plantilla_version
  on public.roles
  for each row execute function public.trg_roles_autoridad_institucional_estricta();

-- ----------------------------------------------------------------------
-- Permisos de roles: autoridad de administracion + cota de delegacion.
-- ----------------------------------------------------------------------
create or replace function public.trg_roles_permisos_autoridad_institucional_estricta()
returns trigger
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_rol_tipo text;
  v_institucion_id uuid;
  v_permiso_codigo text;
begin
  if auth.uid() is null then
    return case when tg_op = 'DELETE' then old else new end;
  end if;

  -- Para UPDATE se valida primero el lado que se abandona; evita mover una
  -- relacion fuera de una institucion sin autoridad sobre ella.
  if tg_op in ('DELETE', 'UPDATE') then
    select r.tipo, r.institucion_id
      into v_rol_tipo, v_institucion_id
    from public.roles r
    where r.id = old.rol_id;

    if v_rol_tipo = 'institucional'
       and not public.usuario_tiene_permiso_institucional_estricto(
         'identidad.roles.asignar_permisos', v_institucion_id
       ) then
      raise exception 'Se requiere autoridad explicita para modificar permisos del rol.'
        using errcode = '42501';
    end if;
  end if;

  if tg_op in ('INSERT', 'UPDATE') then
    select r.tipo, r.institucion_id, p.codigo
      into v_rol_tipo, v_institucion_id, v_permiso_codigo
    from public.roles r
    join public.permisos p on p.id = new.permiso_id
    where r.id = new.rol_id;

    if v_rol_tipo = 'institucional' then
      if not public.usuario_tiene_permiso_institucional_estricto(
        'identidad.roles.asignar_permisos', v_institucion_id
      ) then
        raise exception 'Se requiere autoridad explicita para modificar permisos del rol.'
          using errcode = '42501';
      end if;

      if not public.usuario_tiene_permiso_institucional_estricto(
        v_permiso_codigo, v_institucion_id
      ) then
        raise exception 'No se puede delegar un permiso fuera del alcance institucional del actor.'
          using errcode = '42501';
      end if;
    end if;
  end if;

  return case when tg_op = 'DELETE' then old else new end;
end
$$;

revoke all on function public.trg_roles_permisos_autoridad_institucional_estricta()
  from public, anon, authenticated;
grant execute on function public.trg_roles_permisos_autoridad_institucional_estricta()
  to service_role;

drop trigger if exists trg_roles_permisos_autoridad_institucional_estricta_before
  on public.roles_permisos;
create trigger trg_roles_permisos_autoridad_institucional_estricta_before
  before insert or delete or update of rol_id, permiso_id
  on public.roles_permisos
  for each row execute function public.trg_roles_permisos_autoridad_institucional_estricta();

-- ----------------------------------------------------------------------
-- Asignaciones de roles institucionales.
-- ----------------------------------------------------------------------
create or replace function public.trg_usuarios_roles_autoridad_institucional_estricta()
returns trigger
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_rol_id uuid;
  v_rol_tipo text;
  v_institucion_id uuid;
  v_marca_desactivacion text;
begin
  if auth.uid() is null then
    return case when tg_op = 'DELETE' then old else new end;
  end if;

  if tg_op = 'UPDATE' then
    select r.tipo into v_rol_tipo from public.roles r where r.id = old.rol_id;
    if v_rol_tipo = 'institucional'
       and (old.rol_id is distinct from new.rol_id
            or old.institucion_id is distinct from new.institucion_id) then
      raise exception 'El rol y la institucion de una asignacion institucional son inmutables.'
        using errcode = '23514';
    end if;
  end if;

  v_rol_id := case when tg_op = 'DELETE' then old.rol_id else new.rol_id end;
  select r.tipo, r.institucion_id
    into v_rol_tipo, v_institucion_id
  from public.roles r
  where r.id = v_rol_id;

  if v_rol_tipo <> 'institucional' then
    return case when tg_op = 'DELETE' then old else new end;
  end if;

  -- La desactivacion de una definicion de rol ya fue autorizada por
  -- identidad.roles.editar. En esa misma transaccion se permite historizar
  -- las asignaciones vinculadas sin exigir un permiso adicional.
  if tg_op = 'UPDATE' and old.activo and not new.activo then
    v_marca_desactivacion := current_setting(
      'schoolmanager.rbac_rol_desactivado', true
    );
    if v_marca_desactivacion = v_rol_id::text then
      return new;
    end if;
  end if;

  if not public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.asignar_roles', v_institucion_id
  ) then
    raise exception 'Se requiere autoridad explicita en la institucion para asignar o retirar roles.'
      using errcode = '42501';
  end if;

  return case when tg_op = 'DELETE' then old else new end;
end
$$;

revoke all on function public.trg_usuarios_roles_autoridad_institucional_estricta()
  from public, anon, authenticated;
grant execute on function public.trg_usuarios_roles_autoridad_institucional_estricta()
  to service_role;

drop trigger if exists trg_usuarios_roles_autoridad_institucional_estricta_before
  on public.usuarios_roles;
create trigger trg_usuarios_roles_autoridad_institucional_estricta_before
  before insert or delete or update of rol_id, institucion_id, activo
  on public.usuarios_roles
  for each row execute function public.trg_usuarios_roles_autoridad_institucional_estricta();

insert into public.schema_migrations(version, nombre, checksum)
values ('036', 'rbac_autoridad_institucional_estricta', null)
on conflict (version) do nothing;

commit;

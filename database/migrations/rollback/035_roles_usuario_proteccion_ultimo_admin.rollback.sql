begin;

-- Restaura exactamente el contrato de 028: mantiene la proteccion del ultimo
-- Superadministrador y revierte solo la guarda institucional introducida en 035.
create or replace function public.rpc_desactivar_rol_usuario(
  p_usuario_rol_id uuid, p_motivo text
)
returns void
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_institucion_id uuid;
  v_usuario_id uuid;
  v_rol_tipo text;
  v_rol_codigo text;
  v_superadmins_activos integer;
begin
  if p_motivo is null or btrim(p_motivo) = '' then
    raise exception 'El motivo es obligatorio.' using errcode = '22023';
  end if;

  select ur.institucion_id, ur.usuario_id, r.tipo, r.codigo
    into v_institucion_id, v_usuario_id, v_rol_tipo, v_rol_codigo
  from public.usuarios_roles ur
  join public.roles r on r.id = ur.rol_id
  where ur.id = p_usuario_rol_id and ur.activo
  for update of ur;

  if not found then
    raise exception 'La asignacion activa no existe.' using errcode = 'P0002';
  end if;

  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  if v_rol_tipo = 'plataforma' then
    if v_rol_codigo = 'platform_admin' then
      if not public.usuario_tiene_permiso_actual('platform.superadmins.gestionar', null) then
        raise exception 'Solo un Superadministrador puede retirar platform_admin.'
          using errcode = '42501';
      end if;

      if exists (select 1 from public.usuarios where id = v_usuario_id and activo) then
        select count(*)::integer
          into v_superadmins_activos
        from public.usuarios_roles ur
        join public.roles r on r.id = ur.rol_id
        join public.usuarios u on u.id = ur.usuario_id
        where ur.activo
          and ur.institucion_id is null
          and r.activo
          and r.tipo = 'plataforma'
          and r.codigo = 'platform_admin'
          and u.activo;

        if v_superadmins_activos <= 1 then
          raise exception 'No se puede retirar el ultimo Superadministrador activo.'
            using errcode = '23514';
        end if;
      end if;
    elsif not public.usuario_tiene_permiso_actual('platform.roles.editar', null) then
      raise exception 'Permiso denegado.' using errcode = '42501';
    end if;
  elsif not public.usuario_tiene_permiso_actual(
    'identidad.usuarios.asignar_roles', v_institucion_id
  ) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  update public.usuarios_roles
  set activo = false,
      fecha_desactivacion = now(),
      motivo_desactivacion = btrim(p_motivo),
      updated_at = now()
  where id = p_usuario_rol_id;
end
$$;

revoke execute on function public.rpc_desactivar_rol_usuario(uuid, text)
  from public, anon;
grant execute on function public.rpc_desactivar_rol_usuario(uuid, text)
  to authenticated, service_role;

delete from public.schema_migrations where version = '035';

commit;

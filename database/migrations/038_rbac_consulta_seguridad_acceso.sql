-- Migracion 038 - consulta administrativa para Configuracion > Seguridad y acceso.
--
-- Expone un snapshot JSON filtrado por institucion para que la API no tenga
-- que reconstruir RBAC con SELECT privilegiados. La funcion usa la autoridad
-- estricta introducida en 036 y nunca acepta el fallback global legacy como
-- permiso para administrar otra institucion.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '037') then
    raise exception 'La migracion 038 requiere la 037 aplicada previamente.';
  end if;
end
$$;

create or replace function public.rpc_obtener_seguridad_acceso(
  p_institucion_id uuid
)
returns jsonb
language plpgsql
stable
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_puede_ver_roles boolean;
  v_puede_ver_usuarios boolean;
  v_roles jsonb := '[]'::jsonb;
  v_plantillas jsonb := '[]'::jsonb;
  v_permisos jsonb := '[]'::jsonb;
  v_asignaciones jsonb := '[]'::jsonb;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Usuario autenticado no vinculado o inactivo.' using errcode = '42501';
  end if;

  if p_institucion_id is null or not exists (
    select 1 from public.instituciones i
    where i.id = p_institucion_id and i.activo
  ) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;

  v_puede_ver_roles := public.usuario_tiene_permiso_institucional_estricto(
    'identidad.roles.ver', p_institucion_id
  );
  v_puede_ver_usuarios := public.usuario_tiene_permiso_institucional_estricto(
    'identidad.usuarios.ver', p_institucion_id
  );

  if not v_puede_ver_roles and not v_puede_ver_usuarios then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  if v_puede_ver_roles then
    select coalesce(jsonb_agg(
      jsonb_build_object(
        'id', r.id,
        'codigo', r.codigo,
        'nombre', r.nombre,
        'descripcion', r.descripcion,
        'activo', r.activo,
        'protegido', r.protegido,
        'rolBaseId', r.rol_base_id,
        'plantillaVersion', r.plantilla_version,
        'permisos', coalesce((
          select jsonb_agg(p.codigo order by p.codigo)
          from public.roles_permisos rp
          join public.permisos p on p.id = rp.permiso_id
          where rp.rol_id = r.id
            and p.ambito = 'institucion'
            and p.estado = 'vigente'
            and p.delegable
            and p.visible_en_roles
        ), '[]'::jsonb)
      ) order by r.nombre, r.codigo
    ), '[]'::jsonb)
    into v_roles
    from public.roles r
    where r.tipo = 'institucional'
      and r.institucion_id = p_institucion_id;

    select coalesce(jsonb_agg(
      jsonb_build_object(
        'id', r.id,
        'codigo', r.codigo,
        'nombre', r.nombre,
        'descripcion', r.descripcion,
        'version', r.plantilla_version,
        'clonable', not exists (
          select 1
          from public.roles_permisos rp
          join public.permisos p on p.id = rp.permiso_id
          where rp.rol_id = r.id
            and p.ambito = 'institucion'
            and p.estado = 'vigente'
            and p.delegable
            and not public.usuario_tiene_permiso_institucional_estricto(
              p.codigo, p_institucion_id
            )
        )
      ) order by r.nombre, r.codigo
    ), '[]'::jsonb)
    into v_plantillas
    from public.roles r
    where r.tipo = 'plantilla'
      and r.institucion_id is null
      and r.activo;

    select coalesce(jsonb_agg(
      jsonb_build_object(
        'codigo', p.codigo,
        'modulo', p.modulo,
        'nombre', p.nombre,
        'descripcion', p.descripcion,
        'riesgo', p.riesgo
      ) order by p.modulo, p.nombre, p.codigo
    ), '[]'::jsonb)
    into v_permisos
    from public.permisos p
    where p.ambito = 'institucion'
      and p.estado = 'vigente'
      and p.delegable
      and p.visible_en_roles
      and public.usuario_tiene_permiso_institucional_estricto(
        p.codigo, p_institucion_id
      );
  end if;

  if v_puede_ver_usuarios then
    select coalesce(jsonb_agg(
      jsonb_build_object(
        'id', ur.id,
        'usuarioId', u.id,
        'nombre', btrim(concat_ws(' ', pe.nombres, pe.apellidos)),
        'rolId', r.id,
        'rolCodigo', r.codigo,
        'rolNombre', r.nombre,
        'rolTipo', r.tipo,
        'activo', ur.activo,
        'creadoEn', ur.created_at
      ) order by pe.apellidos, pe.nombres, r.nombre, r.codigo
    ), '[]'::jsonb)
    into v_asignaciones
    from public.usuarios_roles ur
    join public.usuarios u on u.id = ur.usuario_id
    left join public.personas pe on pe.id = u.persona_id
    join public.roles r on r.id = ur.rol_id
    where ur.institucion_id = p_institucion_id
      and ur.activo;
  end if;

  return jsonb_build_object(
    'institucionId', p_institucion_id,
    'capacidades', jsonb_build_object(
      'rolesVer', v_puede_ver_roles,
      'rolesCrear', public.usuario_tiene_permiso_institucional_estricto(
        'identidad.roles.crear', p_institucion_id
      ),
      'rolesEditar', public.usuario_tiene_permiso_institucional_estricto(
        'identidad.roles.editar', p_institucion_id
      ),
      'rolesAsignarPermisos', public.usuario_tiene_permiso_institucional_estricto(
        'identidad.roles.asignar_permisos', p_institucion_id
      ),
      'usuariosVer', v_puede_ver_usuarios,
      'usuariosAsignarRoles', public.usuario_tiene_permiso_institucional_estricto(
        'identidad.usuarios.asignar_roles', p_institucion_id
      )
    ),
    'roles', v_roles,
    'plantillas', v_plantillas,
    'permisosDelegables', v_permisos,
    'asignaciones', v_asignaciones
  );
end
$$;

revoke all on function public.rpc_obtener_seguridad_acceso(uuid)
  from public, anon;
grant execute on function public.rpc_obtener_seguridad_acceso(uuid)
  to authenticated, service_role;

insert into public.schema_migrations(version, nombre, checksum)
values ('038', 'rbac_consulta_seguridad_acceso', null)
on conflict (version) do nothing;

commit;

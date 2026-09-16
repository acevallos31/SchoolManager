-- Migracion 037 - lectura RBAC con autoridad institucional estricta.
-- Depende de 036, que introduce usuario_tiene_permiso_institucional_estricto.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '036') then
    raise exception 'La migracion 037 requiere la 036 aplicada previamente.';
  end if;
end
$$;

-- Las policies RLS se evalúan como authenticated y necesitan poder invocar
-- el helper. La funcion es SECURITY DEFINER y solo devuelve un booleano; no
-- expone filas ni permite mutaciones.
grant execute on function public.usuario_tiene_permiso_institucional_estricto(text, uuid)
  to authenticated;

-- Roles institucionales: un admin global legacy no obtiene visibilidad sobre
-- todas las instituciones. Las plantillas/legacy globales siguen siendo
-- catalogos compartidos durante la transicion.
drop policy if exists roles_select on public.roles;
create policy roles_select on public.roles for select to authenticated using (
  (
    tipo = 'plataforma'
    and public.usuario_tiene_permiso_actual('platform.roles.ver', null)
  )
  or (
    tipo = 'institucional'
    and public.usuario_tiene_permiso_institucional_estricto(
      'identidad.roles.ver', institucion_id
    )
  )
  or (
    tipo in ('legacy', 'plantilla')
    and public.usuario_tiene_permiso_en_algun_ambito('identidad.roles.ver')
  )
);

-- El catalogo de permisos institucionales no contiene datos de una escuela
-- concreta; se mantiene compartido para administradores que puedan ver roles.
drop policy if exists permisos_select on public.permisos;
create policy permisos_select on public.permisos for select to authenticated using (
  (
    ambito = 'plataforma'
    and public.usuario_tiene_permiso_actual('platform.permisos.ver', null)
  )
  or (
    ambito = 'institucion'
    and public.usuario_tiene_permiso_en_algun_ambito('identidad.roles.ver')
  )
);

drop policy if exists roles_permisos_select on public.roles_permisos;
create policy roles_permisos_select on public.roles_permisos for select to authenticated using (
  exists (
    select 1
    from public.roles r
    where r.id = roles_permisos.rol_id
      and (
        (
          r.tipo = 'plataforma'
          and public.usuario_tiene_permiso_actual('platform.roles.ver', null)
        )
        or (
          r.tipo = 'institucional'
          and public.usuario_tiene_permiso_institucional_estricto(
            'identidad.roles.ver', r.institucion_id
          )
        )
        or (
          r.tipo in ('legacy', 'plantilla')
          and public.usuario_tiene_permiso_en_algun_ambito('identidad.roles.ver')
        )
      )
  )
);

drop policy if exists usuarios_roles_select on public.usuarios_roles;
create policy usuarios_roles_select on public.usuarios_roles for select to authenticated using (
  usuario_id = public.usuario_actual_id()
  or exists (
    select 1
    from public.roles r
    where r.id = usuarios_roles.rol_id
      and (
        (
          r.tipo = 'plataforma'
          and public.usuario_tiene_permiso_actual('platform.roles.ver', null)
        )
        or (
          r.tipo = 'institucional'
          and public.usuario_tiene_permiso_institucional_estricto(
            'identidad.usuarios.ver', r.institucion_id
          )
        )
        or (
          r.tipo = 'legacy'
          and usuarios_roles.institucion_id is not null
          and public.usuario_tiene_permiso_institucional_estricto(
            'identidad.usuarios.ver', usuarios_roles.institucion_id
          )
        )
        or (
          r.tipo = 'legacy'
          and usuarios_roles.institucion_id is null
          and public.usuario_tiene_permiso_actual('identidad.usuarios.ver', null)
        )
      )
  )
);

insert into public.schema_migrations(version, nombre, checksum)
values ('037', 'rbac_lectura_institucional_estricta', null)
on conflict (version) do nothing;

commit;

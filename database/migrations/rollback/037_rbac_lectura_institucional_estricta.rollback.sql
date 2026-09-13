begin;

-- Restaura las policies definidas por 028.
drop policy if exists roles_select on public.roles;
create policy roles_select on public.roles for select to authenticated using (
  (
    tipo = 'plataforma'
    and public.usuario_tiene_permiso_actual('platform.roles.ver', null)
  )
  or (
    tipo = 'institucional'
    and public.usuario_tiene_permiso_actual('identidad.roles.ver', institucion_id)
  )
  or (
    tipo in ('legacy', 'plantilla')
    and public.usuario_tiene_permiso_en_algun_ambito('identidad.roles.ver')
  )
);

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
        (r.tipo = 'plataforma'
         and public.usuario_tiene_permiso_actual('platform.roles.ver', null))
        or (r.tipo = 'institucional'
            and public.usuario_tiene_permiso_actual('identidad.roles.ver', r.institucion_id))
        or (r.tipo in ('legacy', 'plantilla')
            and public.usuario_tiene_permiso_en_algun_ambito('identidad.roles.ver'))
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
        (r.tipo = 'plataforma'
         and public.usuario_tiene_permiso_actual('platform.roles.ver', null))
        or (r.tipo <> 'plataforma'
            and public.usuario_tiene_permiso_actual(
              'identidad.usuarios.ver', usuarios_roles.institucion_id
            ))
      )
  )
);

revoke execute on function public.usuario_tiene_permiso_institucional_estricto(text, uuid)
  from authenticated;

delete from public.schema_migrations where version = '037';

commit;

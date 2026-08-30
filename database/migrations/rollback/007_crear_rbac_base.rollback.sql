-- Rollback 007. Conserva usuarios.rol y revierte solo el estado base generado.
-- Se bloquea si existen roles, permisos o asignaciones fuera del seed conocido.
do $$
begin
  if exists (
    select 1 from public.roles
    where codigo not in ('admin', 'operador', 'usuario', 'padre', 'docente', 'cajero', 'consulta')
  ) or exists (
    select 1 from public.permisos
    where codigo not in (
      'academico.alumnos.ver',
      'academico.alumnos.crear',
      'academico.alumnos.editar',
      'academico.alumnos.desactivar',
      'academico.matriculas.ver',
      'academico.matriculas.crear',
      'academico.matriculas.editar',
      'academico.matriculas.anular',
      'academico.matriculas.cambiar_estado',
      'academico.secciones.ver',
      'academico.secciones.crear',
      'academico.secciones.editar',
      'academico.secciones.desactivar',
      'academico.responsables.ver',
      'academico.responsables.crear',
      'academico.responsables.editar',
      'responsables.responsables.ver',
      'responsables.responsables.crear',
      'responsables.responsables.editar',
      'identidad.usuarios.ver',
      'identidad.usuarios.crear',
      'identidad.usuarios.editar',
      'identidad.usuarios.asignar_roles',
      'identidad.roles.ver',
      'identidad.roles.crear',
      'identidad.roles.editar',
      'identidad.roles.asignar_permisos'
    )
  ) or exists (
    select 1
    from public.usuarios_roles ur
    join public.roles r on r.id = ur.rol_id
    join public.usuarios u on u.id = ur.usuario_id
    where ur.institucion_id is not null
       or not ur.activo
       or r.codigo <> u.rol
  ) or exists (
    select 1
    from public.roles_permisos rp
    join public.roles r on r.id = rp.rol_id
    join public.permisos p on p.id = rp.permiso_id
    where r.codigo not in ('admin', 'operador')
       or (
         r.codigo = 'operador'
         and p.codigo not in (
           'academico.alumnos.ver',
           'academico.alumnos.crear',
           'academico.alumnos.editar',
           'academico.matriculas.ver',
           'academico.matriculas.crear',
           'academico.matriculas.cambiar_estado',
           'academico.secciones.ver',
           'academico.secciones.crear',
           'academico.secciones.editar',
           'academico.responsables.ver',
           'academico.responsables.crear',
           'academico.responsables.editar',
           'responsables.responsables.ver',
           'responsables.responsables.crear',
           'responsables.responsables.editar'
         )
       )
  ) then
    raise exception 'Rollback 007 bloqueado: existen datos RBAC posteriores al seed inicial.';
  end if;
end;
$$;

drop function if exists public.usuario_tiene_permiso(uuid, text, uuid);
delete from public.schema_migrations where version = '007';
drop table if exists public.roles_permisos;
drop table if exists public.usuarios_roles;
drop table if exists public.permisos;
drop table if exists public.roles;

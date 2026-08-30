-- Validacion 007: RBAC base, migracion completa y asignaciones activas unicas.
select codigo, nombre, activo from public.roles order by codigo;
select codigo, modulo, nombre from public.permisos order by codigo;

select u.id, u.rol
from public.usuarios u
where not exists (
  select 1
  from public.usuarios_roles ur
  join public.roles r on r.id = ur.rol_id
  where ur.usuario_id = u.id
    and ur.activo = true
    and ur.institucion_id is null
    and r.codigo = u.rol
);

select usuario_id, rol_id, institucion_id, count(*) as cantidad
from public.usuarios_roles
where activo = true
group by usuario_id, rol_id, institucion_id
having count(*) > 1;

select public.usuario_tiene_permiso(
  '00000000-0000-0000-0000-000000000000'::uuid,
  'academico.alumnos.ver',
  null
) as identidad_inexistente_sin_permiso;

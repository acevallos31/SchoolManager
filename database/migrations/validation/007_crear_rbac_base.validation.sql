-- Validacion 007: RBAC base, roles/permisos presentes y asignaciones activas unicas.
-- Contrato: toda consulta debe devolver cero filas cuando el esquema es correcto.

select esperado.codigo as rol_faltante
from (values
  ('admin'), ('operador'), ('usuario'), ('padre'), ('docente'), ('cajero'), ('consulta')
) esperado(codigo)
where not exists (select 1 from public.roles r where r.codigo = esperado.codigo);

select esperado.codigo as permiso_faltante
from (values
  ('academico.alumnos.ver'), ('academico.alumnos.crear'), ('academico.matriculas.ver'),
  ('academico.matriculas.crear'), ('responsables.responsables.ver'), ('identidad.usuarios.ver')
) esperado(codigo)
where not exists (select 1 from public.permisos p where p.codigo = esperado.codigo);

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

select '007' as permiso_concedido_a_identidad_inexistente
where public.usuario_tiene_permiso(
  '00000000-0000-0000-0000-000000000000'::uuid,
  'academico.alumnos.ver',
  null
) = true;

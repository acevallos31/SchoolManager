-- Validacion 039 - una fila o excepcion indica un hallazgo.

do $$
declare
  v_def text;
begin
  if not exists (
    select 1 from public.schema_migrations where version = '039'
  ) then
    raise exception 'VALIDACION 039: version no registrada.';
  end if;

  select pg_get_functiondef(
    'public.usuario_tiene_permiso_actual(text,uuid)'::regprocedure
  ) into v_def;

  if position('academico.ciclos.ver' in v_def) = 0
     or position('academico.ciclos.crear' in v_def) = 0
     or position('academico.ciclos.editar' in v_def) = 0
     or position('academico.ciclos.desactivar' in v_def) = 0
     or position('academico.estructura.ver' in v_def) = 0
     or position('academico.estructura.editar' in v_def) = 0
     or position('academico.estructura.desactivar' in v_def) = 0 then
    raise exception 'VALIDACION 039: el puente canonico no contiene todos los permisos esperados.';
  end if;
end
$$;

-- Los aliases internos deben seguir fuera del editor dinámico de roles.
select codigo
from public.permisos
where (
    codigo like 'configuracion.ciclos.%'
    or codigo like 'configuracion.periodos_matricula.%'
    or codigo like 'configuracion.grados.%'
    or codigo like 'configuracion.jornadas.%'
    or codigo like 'configuracion.secciones.%'
  )
  and (delegable or visible_en_roles);

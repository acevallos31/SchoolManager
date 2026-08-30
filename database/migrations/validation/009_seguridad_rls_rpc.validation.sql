-- Validacion 009. Las consultas de diagnostico deben devolver cero filas.
select c.relname as tabla_sin_rls
from (values
  ('personas'), ('usuarios'), ('roles'), ('permisos'), ('usuarios_roles'),
  ('roles_permisos'), ('alumnos'), ('responsables'), ('alumno_responsable'),
  ('ciclos_escolares'), ('grados'), ('jornadas'), ('secciones'),
  ('periodos_matricula'), ('matriculas'), ('matricula_estado_historial')
) esperado(nombre)
join pg_class c on c.relname = esperado.nombre and c.relnamespace = 'public'::regnamespace
where not c.relrowsecurity;

select esperado.nombre as policy_faltante
from (values
  ('personas_select'), ('usuarios_select'), ('roles_select'), ('permisos_select'),
  ('usuarios_roles_select'), ('roles_permisos_select'), ('alumnos_select'),
  ('responsables_select'), ('alumno_responsable_select'), ('ciclos_select'),
  ('grados_select'), ('jornadas_select'), ('secciones_select'),
  ('periodos_matricula_select'), ('matriculas_select'), ('matricula_historial_select')
) esperado(nombre)
where not exists (
  select 1 from pg_policies
  where schemaname = 'public' and policyname = esperado.nombre
);

select table_name, privilege_type
from information_schema.role_table_grants
where grantee = 'authenticated'
  and table_schema = 'public'
  and privilege_type in ('INSERT', 'UPDATE', 'DELETE', 'TRUNCATE', 'REFERENCES', 'TRIGGER');

select table_name, privilege_type
from information_schema.role_table_grants
where grantee = 'anon' and table_schema = 'public';

select routine_name
from information_schema.routine_privileges
where grantee = 'authenticated'
  and specific_schema = 'public'
  and routine_name in (
    'crear_seccion', 'matricular_alumno', 'cambiar_estado_matricula',
    'desactivar_alumno', 'reactivar_alumno', 'desactivar_seccion',
    'crear_alumno_nueva_persona', 'crear_alumno_para_persona'
  );

select esperado.nombre as rpc_sin_execute
from (values
  ('rpc_crear_alumno_nueva_persona'), ('rpc_crear_alumno_para_persona'),
  ('rpc_crear_seccion'), ('rpc_matricular_alumno'),
  ('rpc_cambiar_estado_matricula'), ('rpc_desactivar_alumno'),
  ('rpc_reactivar_alumno'), ('rpc_desactivar_seccion'),
  ('rpc_asignar_rol_usuario'), ('rpc_desactivar_rol_usuario')
) esperado(nombre)
where not exists (
  select 1 from information_schema.routine_privileges
  where grantee = 'authenticated' and specific_schema = 'public'
    and routine_name = esperado.nombre and privilege_type = 'EXECUTE'
);

select version, nombre from public.schema_migrations where version = '009';

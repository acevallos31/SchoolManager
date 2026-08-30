-- Las consultas de diagnostico deben devolver cero filas, salvo el registro final.
select 'funcion interna faltante' as error
where to_regprocedure(
  'public.crear_alumno_nueva_persona_con_documento(uuid,text,text,text,text,date,text,text)'
) is null;

select 'RPC segura faltante' as error
where to_regprocedure(
  'public.rpc_crear_alumno_nueva_persona_con_documento(uuid,text,text,text,text,date,text,text)'
) is null;

select 'authenticated no puede ejecutar RPC' as error
where not has_function_privilege(
  'authenticated',
  'public.rpc_crear_alumno_nueva_persona_con_documento(uuid,text,text,text,text,date,text,text)',
  'EXECUTE'
);

select 'authenticated puede ejecutar funcion interna' as error
where has_function_privilege(
  'authenticated',
  'public.crear_alumno_nueva_persona_con_documento(uuid,text,text,text,text,date,text,text)',
  'EXECUTE'
);

select version, nombre from public.schema_migrations where version = '011';

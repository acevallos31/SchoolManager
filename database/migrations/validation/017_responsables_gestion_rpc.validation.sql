-- Validacion 017: diagnosticos (filas = hallazgos) para la gestion RPC de responsables.
-- Correccion post-auditoria externa (fase 018 / PR #31). Ejecutar contra una
-- base que tenga aplicada la 017 (p. ej. la de los IntegrationTests), no en
-- Supabase produccion.

-- (1) La migracion 017 quedo registrada en schema_migrations.
select '017 no registrado en schema_migrations' error
where not exists (select 1 from public.schema_migrations where version = '017');

-- (2) Indice de cobertura nuevo: responsables por institucion.
select 'ix_responsables_institucion_id no existe' error
where to_regclass('public.ix_responsables_institucion_id') is null;

-- (3) Todos los RPC esperados existen.
select firma_rpc from (values
  ('rpc_crear_responsable_con_documento(uuid,text,text,text,text,text,text)'),
  ('rpc_crear_responsable_para_persona(uuid,uuid)'),
  ('rpc_editar_responsable(uuid,text,text,text,text)'),
  ('rpc_inactivar_responsable(uuid,text)'),
  ('rpc_reactivar_responsable(uuid)'),
  ('rpc_vincular_alumno_responsable(uuid,uuid,text,boolean,boolean)'),
  ('rpc_editar_vinculo_responsable(uuid,text,boolean,boolean)'),
  ('rpc_desactivar_vinculo_responsable(uuid,text)'),
  ('rpc_reactivar_vinculo_responsable(uuid)')
) esperado(firma_rpc)
where to_regprocedure('public.' || esperado.firma_rpc) is null;

-- (4) Seguridad EXECUTE: los RPC (security definer) NO deben quedar
-- ejecutables por anon/public; deben serlo por authenticated.
-- 4a) ningun RPC ejecutable por anon.
select 'RPC ejecutable por anon: ' || p.oid::regprocedure::text error
from pg_proc p
join pg_namespace n on n.oid = p.pronamespace
where n.nspname = 'public'
  and p.proname like 'rpc_%responsable%'
  and has_function_privilege('anon', p.oid, 'EXECUTE');
-- 4b) ningun RPC ejecutable por la pseudo-rol public.
select 'RPC ejecutable por public: ' || p.oid::regprocedure::text error
from pg_proc p
join pg_namespace n on n.oid = p.pronamespace
where n.nspname = 'public'
  and p.proname like 'rpc_%responsable%'
  and has_function_privilege('public', p.oid, 'EXECUTE');
-- 4c) todo RPC de escritura ejecutable por authenticated.
select firma_rpc from (values
  ('rpc_crear_responsable_con_documento(uuid,text,text,text,text,text,text)'),
  ('rpc_crear_responsable_para_persona(uuid,uuid)'),
  ('rpc_editar_responsable(uuid,text,text,text,text)'),
  ('rpc_inactivar_responsable(uuid,text)'),
  ('rpc_reactivar_responsable(uuid)'),
  ('rpc_vincular_alumno_responsable(uuid,uuid,text,boolean,boolean)'),
  ('rpc_editar_vinculo_responsable(uuid,text,boolean,boolean)'),
  ('rpc_desactivar_vinculo_responsable(uuid,text)'),
  ('rpc_reactivar_vinculo_responsable(uuid)')
) esperado(firma_rpc)
where not has_function_privilege('authenticated', to_regprocedure('public.' || esperado.firma_rpc), 'EXECUTE');

-- (5) Funciones internas (security invoker) NO ejecutables por anon/public/authenticated.
select 'Funcion interna ejecutable por anon: ' || p.oid::regprocedure::text error
from pg_proc p
join pg_namespace n on n.oid = p.pronamespace
where n.nspname = 'public'
  and p.proname in (
    'crear_o_reutilizar_persona_con_documento', 'crear_responsable',
    'editar_datos_persona', 'inactivar_responsable', 'reactivar_responsable',
    'vincular_alumno_responsable', 'editar_vinculo_responsable',
    'desactivar_vinculo_responsable', 'reactivar_vinculo_responsable')
  and has_function_privilege('anon', p.oid, 'EXECUTE');
select 'Funcion interna ejecutable por authenticated: ' || p.oid::regprocedure::text error
from pg_proc p
join pg_namespace n on n.oid = p.pronamespace
where n.nspname = 'public'
  and p.proname in (
    'crear_o_reutilizar_persona_con_documento', 'crear_responsable',
    'editar_datos_persona', 'inactivar_responsable', 'reactivar_responsable',
    'vincular_alumno_responsable', 'editar_vinculo_responsable',
    'desactivar_vinculo_responsable', 'reactivar_vinculo_responsable')
  and has_function_privilege('authenticated', p.oid, 'EXECUTE');

-- (6) Invariantes de negocio vigentes.
select 'Vinculos inactivos con es_principal=true' error
where exists (
  select 1 from public.alumno_responsable
  where estado = 'inactivo' and es_principal = true
);

select 'Responsable inactivo con vinculos activos' error
where exists (
  select 1 from public.alumno_responsable ar
  join public.responsables r on r.id = ar.responsable_id
  where r.estado = 'inactivo' and ar.estado = 'activo'
);

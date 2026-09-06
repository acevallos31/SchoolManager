-- Validacion Migracion 020: grados/jornadas por institucion.
-- Los diagnosticos con sufijo *_faltante o *_indebido deben devolver cero filas.

-- 1. Registro de la migracion.
select '020' as migracion_no_registrada
where not exists (select 1 from public.schema_migrations where version='020');

-- 2. Columnas institucion_id presentes y NOT NULL.
select 'grados_sin_institucion' as columna_faltante
from information_schema.columns
where table_schema='public' and table_name='grados' and column_name='institucion_id'
  and (is_nullable = 'YES' or data_type <> 'uuid');
select 'jornadas_sin_institucion' as columna_faltante
from information_schema.columns
where table_schema='public' and table_name='jornadas' and column_name='institucion_id'
  and (is_nullable = 'YES' or data_type <> 'uuid');

-- 3. FK a instituciones presentes.
select 'fk_grados_institucion_faltante' as error
where not exists (select 1 from pg_constraint where conname='fk_grados_institucion');
select 'fk_jornadas_institucion_faltante' as error
where not exists (select 1 from pg_constraint where conname='fk_jornadas_institucion');

-- 4. Indices de unicidad por institucion presentes.
select 'ux_grados_institucion_nombre_faltante' as error
where to_regclass('public.ux_grados_institucion_nombre') is null;
select 'ux_jornadas_institucion_nombre_faltante' as error
where to_regclass('public.ux_jornadas_institucion_nombre') is null;

-- 5. Indices/constraints de unicidad globales (pre-020) eliminados.
select 'grados_nombre_key_indebido' as error
where exists (select 1 from pg_constraint where conname='grados_nombre_key');
select 'jornadas_nombre_key_indebido' as error
where exists (select 1 from pg_constraint where conname='jornadas_nombre_key');
select 'ux_grados_nombre_normalizado_indebido' as error
where to_regclass('public.ux_grados_nombre_normalizado') is not null;
select 'ux_jornadas_nombre_normalizado_indebido' as error
where to_regclass('public.ux_jornadas_nombre_normalizado') is not null;

-- 6. FK compuesta de secciones -> contexto institucional.
select 'fk_secciones_grado_contexto_faltante' as error
where not exists (select 1 from pg_constraint where conname='fk_secciones_grado_contexto');
select 'fk_secciones_jornada_contexto_faltante' as error
where not exists (select 1 from pg_constraint where conname='fk_secciones_jornada_contexto');

-- 7. RLS de grados/jornadas scoped a la institucion de la fila.
select 'grados_select_no_scoped' as error
from pg_policies
where schemaname='public' and tablename='grados' and policyname='grados_select'
  and (qual is null or qual::text not like '%institucion_id%');
select 'jornadas_select_no_scoped' as error
from pg_policies
where schemaname='public' and tablename='jornadas' and policyname='jornadas_select'
  and (qual is null or qual::text not like '%institucion_id%');

-- 8. RPC de grados/jornadas filtran por institucion (no devuelven el catalogo global).
select 'rpc_listar_grados_no_scoped' as error
from pg_proc p join pg_namespace n on n.oid=p.pronamespace
where n.nspname='public' and p.proname='rpc_listar_grados'
  and pg_get_functiondef(p.oid) not like '%g.institucion_id%';
select 'rpc_listar_jornadas_no_scoped' as error
from pg_proc p join pg_namespace n on n.oid=p.pronamespace
where n.nspname='public' and p.proname='rpc_listar_jornadas'
  and pg_get_functiondef(p.oid) not like '%j.institucion_id%';

-- 9. anon/public no ejecutan las RPC de grados/jornadas/secciones.
select roles.rol, fn.fn as rpc_expuesta
from (values ('anon'), ('public')) roles(rol)
cross join (values
  ('public.rpc_listar_grados(uuid)'),('public.rpc_crear_grado(text,integer,uuid)'),
  ('public.rpc_actualizar_grado(uuid,text,integer,uuid)'),
  ('public.rpc_cambiar_estado_grado(uuid,boolean,uuid)'),
  ('public.rpc_listar_jornadas(uuid)'),('public.rpc_crear_jornada(text,uuid)'),
  ('public.rpc_actualizar_jornada(uuid,text,uuid)'),
  ('public.rpc_cambiar_estado_jornada(uuid,boolean,uuid)')) fn(fn)
where has_function_privilege(roles.rol, fn.fn, 'EXECUTE');

-- 10. Todas las RPC de grados/jornadas/secciones son SECURITY DEFINER con search_path fijo.
select esperado.fn as rpc_configuracion_insegura
from (values
  ('public.rpc_listar_grados(uuid)'),('public.rpc_crear_grado(text,integer,uuid)'),
  ('public.rpc_actualizar_grado(uuid,text,integer,uuid)'),
  ('public.rpc_cambiar_estado_grado(uuid,boolean,uuid)'),
  ('public.rpc_desactivar_grado(uuid,uuid)'),('public.rpc_reactivar_grado(uuid,uuid)'),
  ('public.rpc_listar_jornadas(uuid)'),('public.rpc_crear_jornada(text,uuid)'),
  ('public.rpc_actualizar_jornada(uuid,text,uuid)'),
  ('public.rpc_cambiar_estado_jornada(uuid,boolean,uuid)'),
  ('public.rpc_desactivar_jornada(uuid,uuid)'),('public.rpc_reactivar_jornada(uuid,uuid)'),
  ('public.rpc_listar_secciones(uuid,uuid)'),('public.rpc_crear_seccion(uuid,uuid,uuid,uuid,text,integer)'),
  ('public.rpc_actualizar_seccion(uuid,uuid,uuid,uuid,text,integer,uuid)'),
  ('public.rpc_desactivar_seccion(uuid,text,uuid)'),('public.rpc_reactivar_seccion(uuid,uuid)')) esperado(fn)
join pg_proc p on p.oid = to_regprocedure(esperado.fn)
where not p.prosecdef
   or not coalesce(p.proconfig, array[]::text[])
          @> array['search_path=pg_catalog, public, pg_temp'];

-- 11. La migracion registrada exactamente una vez.
select '020' as migracion_duplicada
from public.schema_migrations
where version='020'
group by version having count(*) > 1;

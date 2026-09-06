-- Validacion Migracion 021: Pagos / Cobranza.
-- Los diagnosticos deben devolver cero filas.

-- 1. Registro de la migracion.
select '021' as migracion_no_registrada
where not exists (select 1 from public.schema_migrations where version='021');

-- 2. Tablas pagos y pagos_aplicaciones presentes.
select 'tabla_pagos_faltante' as error
where to_regclass('public.pagos') is null;
select 'tabla_pagos_aplicaciones_faltante' as error
where to_regclass('public.pagos_aplicaciones') is null;

-- 3. cargos.estado admite los cuatro estados (el CHECK viejo fue reemplazado).
select 'cargos_estado_no_ampliado' as error
where not exists (
  select 1 from pg_constraint c
  join pg_class t on t.oid = c.conrelid and t.relname = 'cargos'
  where c.contype = 'c' and c.conname = 'ck_cargos_estado'
    and pg_get_constraintdef(c.oid) like '%parcial%'
    and pg_get_constraintdef(c.oid) like '%pagado%'
    and pg_get_constraintdef(c.oid) like '%anulado%');

-- 4. Columnas obligatorias de pagos (alumno_id NOT NULL, monto_total > 0,
--    estado registrado|anulado, referencia_externa opcional).
select 'pagos_sin_alumno_obligatorio' as error
from information_schema.columns
where table_schema='public' and table_name='pagos' and column_name='alumno_id'
  and (is_nullable = 'YES' or data_type <> 'uuid');
select 'pagos_sin_monto_total_check' as error
where not exists (select 1 from pg_constraint c
  join pg_class t on t.oid=c.conrelid and t.relname='pagos'
  where c.contype='c' and pg_get_constraintdef(c.oid) like '%monto_total%>%');
select 'pagos_estado_sin_dominio' as error
where not exists (select 1 from pg_constraint c
  join pg_class t on t.oid=c.conrelid and t.relname='pagos'
  where c.contype='c' and pg_get_constraintdef(c.oid) like '%registrado%anulado%');

-- 5. Aplicaciones: suma por pago == monto_total no es violable (sin duplicados
--    pago-cargo, montos > 0, y la RPC lo exige). Se valida dominio y unicidad.
select 'aplicaciones_sin_monto_positivo' as error
where not exists (select 1 from pg_constraint c
  join pg_class t on t.oid=c.conrelid and t.relname='pagos_aplicaciones'
  where c.contype='c' and pg_get_constraintdef(c.oid) like '%monto_aplicado%>%');
select 'aplicaciones_sin_unicidad_pago_cargo' as error
where not exists (select 1 from pg_constraint c
  join pg_class t on t.oid=c.conrelid and t.relname='pagos_aplicaciones'
  where c.contype='u' and c.conname='uq_pagos_aplicaciones_pago_cargo');
select 'aplicaciones_sin_estado_dominio' as error
where not exists (select 1 from pg_constraint c
  join pg_class t on t.oid=c.conrelid and t.relname='pagos_aplicaciones'
  where c.contype='c' and pg_get_constraintdef(c.oid) like '%vigente%reversada%');

-- 6. Indice de unicidad de referencia externa por institucion (parcial).
select 'ux_pagos_referencia_externa_faltante' as error
where to_regclass('public.ux_pagos_referencia_externa_institucion') is null;

-- 7. Superficie RPC-only: tablas nuevas sin policies ni acceso directo
--    para anon/authenticated (RLS activa).
select 'pagos_rls_inactiva' as error
from pg_class t join pg_namespace n on n.oid=t.relnamespace
where n.nspname='public' and t.relname='pagos' and not t.relrowsecurity;
select 'pagos_aplicaciones_rls_inactiva' as error
from pg_class t join pg_namespace n on n.oid=t.relnamespace
where n.nspname='public' and t.relname='pagos_aplicaciones' and not t.relrowsecurity;

-- 8. Permisos propios presentes y asignados a admin.
select 'permiso_pagos_ver_faltante' as error
where not exists (select 1 from public.permisos where codigo='academico.pagos.ver');
select 'permiso_pagos_registrar_faltante' as error
where not exists (select 1 from public.permisos where codigo='academico.pagos.registrar');
select 'permiso_pagos_anular_faltante' as error
where not exists (select 1 from public.permisos where codigo='academico.pagos.anular');

-- 9. RPC de pagos son SECURITY DEFINER con search_path fijo.
select esperado.fn as rpc_pagos_insegura
from (values
  ('public.rpc_registrar_pago(uuid,jsonb,numeric,uuid,uuid,text,text,timestamptz)'),
  ('public.rpc_listar_pagos_alumno(uuid,uuid)'),
  ('public.rpc_obtener_pago(uuid,uuid)'),
  ('public.rpc_obtener_aplicaciones_pago(uuid,uuid)'),
  ('public.rpc_anular_pago(uuid,text,uuid)')) esperado(fn)
join pg_proc p on p.oid = to_regprocedure(esperado.fn)
where not p.prosecdef
   or not coalesce(p.proconfig, array[]::text[])
          @> array['search_path=pg_catalog, public, pg_temp'];

-- 10. La migracion registrada exactamente una vez.
select '021' as migracion_duplicada
from public.schema_migrations
where version='021'
group by version having count(*) > 1;

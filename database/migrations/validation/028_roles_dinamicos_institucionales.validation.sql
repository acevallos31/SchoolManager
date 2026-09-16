-- Validacion 028 - cada fila devuelta representa un hallazgo.
-- Solo lectura: no crea roles, no asigna usuarios y no modifica permisos.

-- 1. Registro de migracion.
select '028_no_registrada' as error
where not exists (
  select 1 from public.schema_migrations where version = '028'
);

select '028_duplicada' as error
from public.schema_migrations
where version = '028'
group by version
having count(*) > 1;

-- 2. Metadatos de permisos.
select '028_columna_permisos_faltante:' || esperado.columna as error
from (values
  ('ambito'), ('delegable'), ('riesgo'), ('estado'), ('visible_en_roles')
) esperado(columna)
where not exists (
  select 1
  from information_schema.columns c
  where c.table_schema = 'public'
    and c.table_name = 'permisos'
    and c.column_name = esperado.columna
);

select '028_permiso_plataforma_delegable:' || codigo as error
from public.permisos
where ambito = 'plataforma' and delegable;

select '028_permiso_metadata_invalida:' || codigo as error
from public.permisos
where ambito not in ('institucion', 'plataforma')
   or riesgo not in ('bajo', 'medio', 'alto', 'critico')
   or estado not in ('vigente', 'deprecado');

-- 3. Permisos globales protegidos.
select '028_permiso_platform_faltante:' || esperado.codigo as error
from (values
  ('platform.superadmins.gestionar'),
  ('platform.roles.ver'),
  ('platform.roles.editar'),
  ('platform.permisos.ver'),
  ('platform.permisos.editar'),
  ('platform.auditoria.ver')
) esperado(codigo)
where not exists (
  select 1
  from public.permisos p
  where p.codigo = esperado.codigo
    and p.modulo = 'platform'
    and p.ambito = 'plataforma'
    and not p.delegable
    and p.estado = 'vigente'
);

select '028_configuracion_global_no_clasificada:' || codigo as error
from public.permisos
where codigo in (
  'configuracion.sistema.ver',
  'configuracion.sistema.editar',
  'configuracion.instituciones.ver',
  'configuracion.instituciones.editar'
)
  and (ambito <> 'plataforma' or delegable);

select '028_legacy_responsables_visible:' || codigo as error
from public.permisos
where codigo like 'responsables.responsables.%'
  and (estado <> 'deprecado' or delegable or visible_en_roles);

-- 4. Metadatos y unicidad de roles.
select '028_columna_roles_faltante:' || esperado.columna as error
from (values
  ('institucion_id'), ('tipo'), ('rol_base_id'), ('protegido'), ('plantilla_version')
) esperado(columna)
where not exists (
  select 1
  from information_schema.columns c
  where c.table_schema = 'public'
    and c.table_name = 'roles'
    and c.column_name = esperado.columna
);

select '028_indice_roles_global_faltante' as error
where not exists (
  select 1
  from pg_indexes
  where schemaname = 'public'
    and tablename = 'roles'
    and indexname = 'ux_roles_codigo_global'
);

select '028_indice_roles_institucion_faltante' as error
where not exists (
  select 1
  from pg_indexes
  where schemaname = 'public'
    and tablename = 'roles'
    and indexname = 'ux_roles_institucion_codigo'
);

select '028_unicidad_global_legacy_presente' as error
where exists (
  select 1 from pg_constraint
  where conrelid = 'public.roles'::regclass
    and conname = 'uq_roles_codigo'
);

select '028_codigo_global_duplicado:' || codigo as error
from public.roles
where institucion_id is null
group by codigo
having count(*) > 1;

select '028_codigo_institucional_duplicado:' || institucion_id || ':' || codigo as error
from public.roles
where institucion_id is not null
group by institucion_id, codigo
having count(*) > 1;

-- 5. Superadministrador y plantillas.
select '028_platform_admin_invalido' as error
where not exists (
  select 1 from public.roles
  where codigo = 'platform_admin'
    and tipo = 'plataforma'
    and institucion_id is null
    and protegido
    and activo
);

select '028_plantilla_faltante:' || esperado.codigo as error
from (values
  ('school_admin'),
  ('school_staff'),
  ('academic_coordinator'),
  ('finance_operator'),
  ('teacher'),
  ('parent'),
  ('student'),
  ('demo_viewer'),
  ('support_agent')
) esperado(codigo)
where not exists (
  select 1 from public.roles r
  where r.codigo = esperado.codigo
    and r.tipo = 'plantilla'
    and r.institucion_id is null
    and r.protegido
    and r.plantilla_version = 1
);

select '028_plantilla_asignada:' || r.codigo as error
from public.usuarios_roles ur
join public.roles r on r.id = ur.rol_id
where r.tipo = 'plantilla' and ur.activo;

select '028_rol_plataforma_con_institucion:' || r.codigo as error
from public.roles r
where r.tipo = 'plataforma' and r.institucion_id is not null;

select '028_rol_institucional_sin_institucion:' || r.codigo as error
from public.roles r
where r.tipo = 'institucional' and r.institucion_id is null;

-- Ninguna cuenta legacy es promovida automaticamente por la propia migracion.
select '028_promocion_automatica_detectada' as error
where exists (
  select 1
  from public.usuarios_roles ur
  join public.roles r on r.id = ur.rol_id
  where r.codigo = 'platform_admin'
    and ur.created_at <= (
      select aplicado_en from public.schema_migrations where version = '028'
      limit 1
    )
);

-- 6. Los permisos platform.* pertenecen exclusivamente al platform_admin.
select '028_platform_permiso_en_otro_rol:' || r.codigo || ':' || p.codigo as error
from public.roles_permisos rp
join public.roles r on r.id = rp.rol_id
join public.permisos p on p.id = rp.permiso_id
where p.codigo like 'platform.%'
  and r.codigo <> 'platform_admin';

select '028_platform_admin_sin_permiso:' || p.codigo as error
from public.permisos p
where p.codigo like 'platform.%'
  and not exists (
    select 1
    from public.roles r
    join public.roles_permisos rp on rp.rol_id = r.id
    where r.codigo = 'platform_admin'
      and r.tipo = 'plataforma'
      and rp.permiso_id = p.id
  );

select '028_admin_legacy_recibio_platform' as error
where exists (
  select 1
  from public.roles r
  join public.roles_permisos rp on rp.rol_id = r.id
  join public.permisos p on p.id = rp.permiso_id
  where r.codigo = 'admin'
    and p.codigo like 'platform.%'
);

select '028_school_admin_recibio_plataforma' as error
where exists (
  select 1
  from public.roles r
  join public.roles_permisos rp on rp.rol_id = r.id
  join public.permisos p on p.id = rp.permiso_id
  where r.codigo = 'school_admin'
    and r.tipo = 'plantilla'
    and p.ambito = 'plataforma'
);

-- 7. Invariante y RPC endurecidas.
select '028_trigger_ambito_faltante' as error
where not exists (
  select 1
  from pg_trigger
  where tgrelid = 'public.usuarios_roles'::regclass
    and tgname = 'trg_usuarios_roles_validar_ambito_rol_before'
    and not tgisinternal
);

select '028_rpc_asignar_sin_guard_superadmin' as error
where exists (
  select 1
  from pg_proc p
  join pg_namespace n on n.oid = p.pronamespace
  where n.nspname = 'public'
    and p.proname = 'rpc_asignar_rol_usuario'
    and pg_get_functiondef(p.oid) not like '%platform.superadmins.gestionar%'
);

select '028_rpc_desactivar_sin_guard_superadmin' as error
where exists (
  select 1
  from pg_proc p
  join pg_namespace n on n.oid = p.pronamespace
  where n.nspname = 'public'
    and p.proname = 'rpc_desactivar_rol_usuario'
    and (
      pg_get_functiondef(p.oid) not like '%platform.superadmins.gestionar%'
      or pg_get_functiondef(p.oid) not like '%ultimo Superadministrador activo%'
    )
);

-- 8. Ninguna asignacion activa viola el ambito nuevo.
select '028_asignacion_plantilla_activa:' || ur.id as error
from public.usuarios_roles ur
join public.roles r on r.id = ur.rol_id
where ur.activo and r.tipo = 'plantilla';

select '028_asignacion_plataforma_institucional:' || ur.id as error
from public.usuarios_roles ur
join public.roles r on r.id = ur.rol_id
where ur.activo
  and r.tipo = 'plataforma'
  and ur.institucion_id is not null;

select '028_asignacion_rol_institucion_cruzada:' || ur.id as error
from public.usuarios_roles ur
join public.roles r on r.id = ur.rol_id
where ur.activo
  and r.tipo = 'institucional'
  and ur.institucion_id is distinct from r.institucion_id;

-- 9. Policies sensibles siguen presentes.
select '028_policy_faltante:' || esperado.policyname as error
from (values
  ('roles_select'),
  ('permisos_select'),
  ('roles_permisos_select'),
  ('usuarios_roles_select')
) esperado(policyname)
where not exists (
  select 1 from pg_policies p
  where p.schemaname = 'public'
    and p.policyname = esperado.policyname
);

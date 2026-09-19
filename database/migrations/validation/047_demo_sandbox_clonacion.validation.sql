select '047 no registrado' as error
where not exists (
  select 1 from public.schema_migrations where version = '047'
);

select 'falta plantilla demo_operator' as error
where not exists (
  select 1
  from public.roles
  where codigo='demo_operator'
    and tipo='plantilla'
    and institucion_id is null
    and activo
    and protegido
);

select 'demo_operator contiene permisos de identidad' as error
where exists (
  select 1
  from public.roles r
  join public.roles_permisos rp on rp.rol_id=r.id
  join public.permisos p on p.id=rp.permiso_id
  where r.codigo='demo_operator'
    and r.tipo='plantilla'
    and p.codigo like 'identidad.%'
);

select 'demo_operator contiene configuracion.sistema.editar' as error
where exists (
  select 1
  from public.roles r
  join public.roles_permisos rp on rp.rol_id=r.id
  join public.permisos p on p.id=rp.permiso_id
  where r.codigo='demo_operator'
    and r.tipo='plantilla'
    and p.codigo='configuracion.sistema.editar'
);

select 'demo_operator contiene permisos de plataforma' as error
where exists (
  select 1
  from public.roles r
  join public.roles_permisos rp on rp.rol_id=r.id
  join public.permisos p on p.id=rp.permiso_id
  where r.codigo='demo_operator'
    and r.tipo='plantilla'
    and p.ambito='plataforma'
);

select 'rpc_crear_sandbox_demo no existe' as error
where to_regprocedure('public.rpc_crear_sandbox_demo(uuid,uuid)') is null;

select 'rpc_reset_sandbox_demo no existe' as error
where to_regprocedure('public.rpc_reset_sandbox_demo(uuid,uuid)') is null;

select 'rpc_crear_sandbox_demo debe ser SECURITY DEFINER' as error
where exists (
  select 1 from pg_proc p
  join pg_namespace n on n.oid=p.pronamespace
  where n.nspname='public'
    and p.proname='rpc_crear_sandbox_demo'
    and not p.prosecdef
);

select 'rpc_reset_sandbox_demo debe ser SECURITY DEFINER' as error
where exists (
  select 1 from pg_proc p
  join pg_namespace n on n.oid=p.pronamespace
  where n.nspname='public'
    and p.proname='rpc_reset_sandbox_demo'
    and not p.prosecdef
);

select 'authenticated puede ejecutar rpc_crear_sandbox_demo' as error
where has_function_privilege(
  'authenticated','public.rpc_crear_sandbox_demo(uuid,uuid)','EXECUTE'
);

select 'anon puede ejecutar rpc_crear_sandbox_demo' as error
where has_function_privilege(
  'anon','public.rpc_crear_sandbox_demo(uuid,uuid)','EXECUTE'
);

select 'authenticated puede ejecutar rpc_reset_sandbox_demo' as error
where has_function_privilege(
  'authenticated','public.rpc_reset_sandbox_demo(uuid,uuid)','EXECUTE'
);

select 'anon puede ejecutar rpc_reset_sandbox_demo' as error
where has_function_privilege(
  'anon','public.rpc_reset_sandbox_demo(uuid,uuid)','EXECUTE'
);

select 'service_role no puede ejecutar rpc_crear_sandbox_demo' as error
where not has_function_privilege(
  'service_role','public.rpc_crear_sandbox_demo(uuid,uuid)','EXECUTE'
);

select 'service_role no puede ejecutar rpc_reset_sandbox_demo' as error
where not has_function_privilege(
  'service_role','public.rpc_reset_sandbox_demo(uuid,uuid)','EXECUTE'
);


select 'persiste unicidad global legacy de ciclos' as error
where exists (
  select 1
  from pg_constraint
  where conname='ciclos_escolares_nombre_key'
    and conrelid='public.ciclos_escolares'::regclass
);

select 'falta unicidad de ciclo por institucion' as error
where not exists (
  select 1
  from pg_constraint
  where conname='uq_ciclos_escolares_institucion_nombre'
    and conrelid='public.ciclos_escolares'::regclass
);

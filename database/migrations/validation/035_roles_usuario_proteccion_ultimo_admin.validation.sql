select '035 no registrado' as error
where not exists (select 1 from public.schema_migrations where version='035');

select 'rpc_desactivar_rol_usuario no existe' as error
where to_regprocedure('public.rpc_desactivar_rol_usuario(uuid,text)') is null;

select 'seguridad_auditoria no tiene RLS' as error
where not exists (
  select 1 from pg_class c
  join pg_namespace n on n.oid=c.relnamespace
  where n.nspname='public' and c.relname='seguridad_auditoria' and c.relrowsecurity
);

begin;

drop function if exists public.rpc_reset_sandbox_demo(uuid, uuid);
drop function if exists public.rpc_crear_sandbox_demo(uuid, uuid);

delete from public.roles_permisos
where rol_id in (
  select id
  from public.roles
  where codigo='demo_operator'
    and tipo='plantilla'
    and institucion_id is null
);

delete from public.roles
where codigo='demo_operator'
  and tipo='plantilla'
  and institucion_id is null;

delete from public.schema_migrations where version='047';

commit;

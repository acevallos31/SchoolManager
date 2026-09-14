-- Rollback 039 - restaura usuario_tiene_permiso_actual sin puente canonico.
-- Las RPC vuelven a exigir exclusivamente el codigo solicitado por cada una.

begin;

create or replace function public.usuario_tiene_permiso_actual(
  p_permiso_codigo text,
  p_institucion_id uuid default null
)
returns boolean
language sql
stable
security definer
set search_path = pg_catalog, public, pg_temp
as $$
  select coalesce(public.usuario_tiene_permiso(
    auth.uid(), p_permiso_codigo, p_institucion_id
  ), false);
$$;

delete from public.schema_migrations where version = '039';

commit;

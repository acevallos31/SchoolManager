-- Validacion 046: ninguna RPC de negocio SECURITY DEFINER debe quedar
-- ejecutable directamente por authenticated. Los helpers usuario_* usados por
-- RLS deben seguir disponibles.

select '046 no registrado' as error
where not exists (select 1 from public.schema_migrations where version='046');

select 'queda RPC SECURITY DEFINER ejecutable por authenticated: ' || p.oid::regprocedure::text as error
from pg_proc p
join pg_namespace n on n.oid=p.pronamespace
where n.nspname='public'
  and p.prosecdef
  and p.proname like 'rpc_%'
  and has_function_privilege('authenticated',p.oid,'EXECUTE');

with permitidas(firma) as (
  values
    ('public.usuario_actual_id()'),
    ('public.usuario_tiene_permiso_actual(text,uuid)'),
    ('public.usuario_puede_ver_alumno(uuid)'),
    ('public.usuario_puede_ver_institucion(uuid)'),
    ('public.usuario_tiene_permiso_institucional_estricto(text,uuid)'),
    ('public.usuario_puede_ver_ciclo(uuid)'),
    ('public.usuario_puede_ver_matricula(uuid)'),
    ('public.usuario_puede_ver_responsable(uuid)'),
    ('public.usuario_tiene_permiso_en_algun_ambito(text)')
)
select 'helper RLS sin EXECUTE authenticated: ' || firma as error
from permitidas
where to_regprocedure(firma) is null
   or not has_function_privilege('authenticated',to_regprocedure(firma),'EXECUTE');

select 'RPC sin EXECUTE service_role: ' || p.oid::regprocedure::text as error
from pg_proc p
join pg_namespace n on n.oid=p.pronamespace
where n.nspname='public'
  and p.prosecdef
  and p.proname like 'rpc_%'
  and not has_function_privilege('service_role',p.oid,'EXECUTE');

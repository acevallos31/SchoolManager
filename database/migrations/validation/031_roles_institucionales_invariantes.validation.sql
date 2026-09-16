-- Filas devueltas = hallazgos.
select '031 no registrado' as error
where not exists (select 1 from public.schema_migrations where version='031');

select 'contar_admins_institucionales no existe' as error
where to_regprocedure('public.contar_admins_institucionales(uuid)') is null;

select 'trigger de permisos institucionales no existe' as error
where not exists (
  select 1 from pg_trigger
  where tgname='trg_roles_permisos_validar_institucional_before' and not tgisinternal
);

select 'rol institucional contiene permiso fuera de cota' as error
where exists (
  select 1
  from public.roles r
  join public.roles_permisos rp on rp.rol_id=r.id
  join public.permisos p on p.id=rp.permiso_id
  where r.tipo='institucional'
    and (p.ambito<>'institucion' or not p.delegable or p.estado<>'vigente')
);

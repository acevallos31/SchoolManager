-- Migracion 032 - reemplazo transaccional de permisos institucionales.
begin;

do $$ begin
  if not exists (select 1 from public.schema_migrations where version='031') then
    raise exception 'La migracion 032 requiere la 031 aplicada previamente.';
  end if;
end $$;

create or replace function public.rpc_reemplazar_permisos_rol_institucional(
  p_rol_id uuid,
  p_permiso_codigos text[]
)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare
  v_inst uuid;
  v_codigos text[];
begin
  select institucion_id into v_inst
  from public.roles
  where id=p_rol_id and tipo='institucional' and activo
  for update;
  if not found then
    raise exception 'El rol institucional no existe o esta inactivo.' using errcode='P0002';
  end if;
  if public.usuario_actual_id() is null
     or not public.usuario_tiene_permiso_actual('identidad.roles.asignar_permisos',v_inst) then
    raise exception 'Permiso denegado.' using errcode='42501';
  end if;

  select coalesce(array_agg(c order by c),'{}'::text[]) into v_codigos
  from (
    select distinct lower(btrim(x)) c
    from unnest(coalesce(p_permiso_codigos,'{}'::text[])) t(x)
    where x is not null and btrim(x)<>''
  ) s;

  if exists(
    select 1
    from unnest(v_codigos) c
    left join public.permisos p on p.codigo=c
    where p.id is null
       or p.ambito<>'institucion'
       or not p.delegable
       or p.estado<>'vigente'
  ) then
    raise exception 'La seleccion contiene permisos inexistentes o no delegables.' using errcode='23514';
  end if;
end $$;

insert into public.schema_migrations(version,nombre,checksum)
values('032','roles_institucionales_reemplazar_permisos',null)
on conflict(version) do nothing;

commit;

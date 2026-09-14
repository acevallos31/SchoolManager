-- Migracion 031 - invariantes de roles institucionales.
begin;

do $$ begin
  if not exists (select 1 from public.schema_migrations where version='030') then
    raise exception 'La migracion 031 requiere la 030 aplicada previamente.';
  end if;
end $$;

create or replace function public.contar_admins_institucionales(p_institucion_id uuid)
returns integer language sql stable security definer set search_path=pg_catalog,public,pg_temp as $$
  select count(*)::integer
  from public.usuarios u
  where u.activo and public.usuario_es_admin_institucional(u.id,p_institucion_id);
$$;
revoke all on function public.contar_admins_institucionales(uuid) from public,anon,authenticated;
grant execute on function public.contar_admins_institucionales(uuid) to service_role;

create or replace function public.trg_roles_permisos_validar_institucional()
returns trigger language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare v_tipo text; v_ambito text; v_delegable boolean; v_estado text;
begin
  select tipo into v_tipo from public.roles where id=new.rol_id;
  select ambito,delegable,estado into v_ambito,v_delegable,v_estado
  from public.permisos where id=new.permiso_id;

  if v_tipo='institucional'
     and (v_ambito<>'institucion' or not v_delegable or v_estado<>'vigente') then
    raise exception 'Un rol institucional solo puede contener permisos institucionales, vigentes y delegables.'
      using errcode='23514';
  end if;
  return new;
end $$;

revoke all on function public.trg_roles_permisos_validar_institucional() from public,anon,authenticated;
grant execute on function public.trg_roles_permisos_validar_institucional() to service_role;

drop trigger if exists trg_roles_permisos_validar_institucional_before on public.roles_permisos;
create trigger trg_roles_permisos_validar_institucional_before
before insert or update of rol_id,permiso_id on public.roles_permisos
for each row execute function public.trg_roles_permisos_validar_institucional();

insert into public.schema_migrations(version,nombre,checksum)
values('031','roles_institucionales_invariantes',null)
on conflict(version) do nothing;

commit;

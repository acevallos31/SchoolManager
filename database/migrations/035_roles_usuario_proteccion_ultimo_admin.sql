-- Migracion 035 - proteccion al retirar asignaciones.
begin;

do $$ begin
  if not exists (select 1 from public.schema_migrations where version='034') then
    raise exception 'La migracion 035 requiere la 034 aplicada previamente.';
  end if;
end $$;

create or replace function public.rpc_desactivar_rol_usuario(
  p_usuario_rol_id uuid,p_motivo text
)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare v_inst uuid; v_usuario uuid; v_tipo text; v_codigo text;
begin
  if btrim(coalesce(p_motivo,''))='' then raise exception 'El motivo es obligatorio.' using errcode='22023'; end if;
  select ur.institucion_id,ur.usuario_id,r.tipo,r.codigo
  into v_inst,v_usuario,v_tipo,v_codigo
  from public.usuarios_roles ur join public.roles r on r.id=ur.rol_id
  where ur.id=p_usuario_rol_id and ur.activo for update of ur;
  if not found then raise exception 'La asignacion activa no existe.' using errcode='P0002'; end if;
  if public.usuario_actual_id() is null then raise exception 'Permiso denegado.' using errcode='42501'; end if;
end $$;

insert into public.schema_migrations(version,nombre,checksum)
values('035','roles_usuario_proteccion_ultimo_admin',null)
on conflict(version) do nothing;

commit;

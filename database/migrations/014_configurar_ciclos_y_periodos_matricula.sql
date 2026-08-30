-- Administracion segura de ciclos escolares y periodos de matricula.
begin;

do $$ begin
  if not exists (select 1 from public.schema_migrations where version = '013') then
    raise exception 'Migracion 014 requiere la migracion 013.';
  end if;
end $$;

insert into public.permisos (codigo, modulo, nombre) values
 ('configuracion.ciclos.ver','configuracion','Ver ciclos escolares'),
 ('configuracion.ciclos.crear','configuracion','Crear ciclos escolares'),
 ('configuracion.ciclos.editar','configuracion','Editar ciclos escolares'),
 ('configuracion.ciclos.desactivar','configuracion','Desactivar ciclos escolares'),
 ('configuracion.periodos_matricula.ver','configuracion','Ver periodos de matricula'),
 ('configuracion.periodos_matricula.crear','configuracion','Crear periodos de matricula'),
 ('configuracion.periodos_matricula.editar','configuracion','Editar periodos de matricula'),
 ('configuracion.periodos_matricula.desactivar','configuracion','Desactivar periodos de matricula')
on conflict (codigo) do nothing;

insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id from public.roles r cross join public.permisos p
where r.codigo = 'admin' and (p.codigo like 'configuracion.ciclos.%'
  or p.codigo like 'configuracion.periodos_matricula.%')
on conflict do nothing;

create or replace function public.resolver_institucion_operacion(p_institucion_id uuid default null)
returns uuid language plpgsql security definer
set search_path = pg_catalog, public, pg_temp as $$
declare v_multi boolean; v_id uuid;
begin
  select multiples_instituciones into v_multi from public.configuracion_implementacion where id = 1;
  if not found then raise exception 'CONFIGURATION_REQUIRED' using errcode = 'SM001'; end if;
  if v_multi then
    if p_institucion_id is null then
      raise exception 'INSTITUTION_CONTEXT_REQUIRED' using errcode = 'SM003';
    end if;
    v_id := p_institucion_id;
  else
    select institucion_id into v_id from public.resolver_contexto_institucional();
  end if;
  if not exists (select 1 from public.instituciones where id = v_id and activo) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = 'P0002';
  end if;
  return v_id;
end $$;

create or replace function public.rpc_listar_ciclos_escolares(p_institucion_id uuid default null)
returns table(id uuid, institucion_id uuid, nombre text, fecha_inicio date, fecha_fin date,
  activo boolean, fecha_desactivacion timestamptz, motivo_desactivacion text)
language plpgsql security definer set search_path = pg_catalog, public, pg_temp as $$
declare v_institucion uuid;
begin
  if public.usuario_actual_id() is null then raise exception 'Permiso denegado.' using errcode='42501'; end if;
  v_institucion := public.resolver_institucion_operacion(p_institucion_id);
  if not (public.usuario_tiene_permiso_actual('configuracion.ciclos.ver', v_institucion)
    or public.usuario_tiene_permiso_actual('configuracion.ciclos.editar', v_institucion)) then
    raise exception 'Permiso denegado.' using errcode='42501';
  end if;
  return query select c.id,c.institucion_id,c.nombre,c.fecha_inicio,c.fecha_fin,c.activo,
    c.fecha_desactivacion,c.motivo_desactivacion
  from public.ciclos_escolares c where c.institucion_id=v_institucion
  order by c.fecha_inicio desc, c.nombre;
end $$;

create or replace function public.rpc_crear_ciclo_escolar(p_nombre text,p_fecha_inicio date,
  p_fecha_fin date,p_institucion_id uuid default null)
returns uuid language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare v_institucion uuid; v_id uuid;
begin
  if public.usuario_actual_id() is null then raise exception 'Permiso denegado.' using errcode='42501'; end if;
  v_institucion:=public.resolver_institucion_operacion(p_institucion_id);
  if not public.usuario_tiene_permiso_actual('configuracion.ciclos.crear',v_institucion) then
    raise exception 'Permiso denegado.' using errcode='42501'; end if;
  if btrim(coalesce(p_nombre,''))='' or p_fecha_inicio is null or p_fecha_fin is null
    or p_fecha_inicio>p_fecha_fin then
    raise exception 'Nombre y rango de fechas del ciclo no son validos.' using errcode='22023'; end if;
  insert into public.ciclos_escolares(institucion_id,nombre,fecha_inicio,fecha_fin)
  values(v_institucion,btrim(p_nombre),p_fecha_inicio,p_fecha_fin) returning id into v_id;
  return v_id;
end $$;

create or replace function public.rpc_actualizar_ciclo_escolar(p_ciclo_id uuid,p_nombre text,
  p_fecha_inicio date,p_fecha_fin date,p_activo boolean,p_motivo_desactivacion text default null)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare v_institucion uuid; v_activo boolean;
begin
  select institucion_id,activo into v_institucion,v_activo from public.ciclos_escolares where id=p_ciclo_id for update;
  if not found then raise exception 'El ciclo escolar no existe.' using errcode='P0002'; end if;
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    case when v_activo and not p_activo then 'configuracion.ciclos.desactivar'
      else 'configuracion.ciclos.editar' end,v_institucion) then
    raise exception 'Permiso denegado.' using errcode='42501'; end if;
  if btrim(coalesce(p_nombre,''))='' or p_fecha_inicio is null or p_fecha_fin is null
    or p_fecha_inicio>p_fecha_fin then
    raise exception 'Nombre y rango de fechas del ciclo no son validos.' using errcode='22023'; end if;
  if v_activo and not p_activo and btrim(coalesce(p_motivo_desactivacion,''))='' then
    raise exception 'El motivo de desactivacion es obligatorio.' using errcode='22023'; end if;
  if exists(select 1 from public.periodos_matricula pm where pm.ciclo_id=p_ciclo_id
    and (pm.fecha_inicio<p_fecha_inicio or pm.fecha_fin>p_fecha_fin)) then
    raise exception 'El nuevo rango excluye periodos de matricula existentes.' using errcode='22023'; end if;
  update public.ciclos_escolares set nombre=btrim(p_nombre),fecha_inicio=p_fecha_inicio,
    fecha_fin=p_fecha_fin,activo=p_activo,updated_at=now(),
    fecha_desactivacion=case when p_activo then null when v_activo then now() else fecha_desactivacion end,
    motivo_desactivacion=case when p_activo then null when v_activo then btrim(p_motivo_desactivacion) else motivo_desactivacion end
  where id=p_ciclo_id;
end $$;

create or replace function public.rpc_desactivar_ciclo_escolar(p_ciclo_id uuid,p_motivo text)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare c public.ciclos_escolares%rowtype;
begin select * into c from public.ciclos_escolares where id=p_ciclo_id;
  if not found then raise exception 'El ciclo escolar no existe.' using errcode='P0002'; end if;
  perform public.rpc_actualizar_ciclo_escolar(c.id,c.nombre,c.fecha_inicio,c.fecha_fin,false,p_motivo);
end $$;

create or replace function public.rpc_reactivar_ciclo_escolar(p_ciclo_id uuid)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare c public.ciclos_escolares%rowtype;
begin select * into c from public.ciclos_escolares where id=p_ciclo_id;
  if not found then raise exception 'El ciclo escolar no existe.' using errcode='P0002'; end if;
  perform public.rpc_actualizar_ciclo_escolar(c.id,c.nombre,c.fecha_inicio,c.fecha_fin,true,null);
end $$;

create or replace function public.rpc_listar_periodos_matricula(p_ciclo_id uuid)
returns table(id uuid,ciclo_id uuid,nombre text,tipo text,fecha_inicio date,fecha_fin date,activo boolean)
language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare v_institucion uuid; v_contexto uuid;
begin
  select c.institucion_id into v_institucion from public.ciclos_escolares c where c.id=p_ciclo_id;
  if not found then raise exception 'El ciclo escolar no existe.' using errcode='P0002'; end if;
  v_contexto:=public.resolver_institucion_operacion(null);
  if v_institucion<>v_contexto then raise exception 'El ciclo escolar no pertenece a la institucion actual.' using errcode='P0002'; end if;
  if public.usuario_actual_id() is null or not (
    public.usuario_tiene_permiso_actual('configuracion.periodos_matricula.ver',v_institucion)
    or public.usuario_tiene_permiso_actual('configuracion.periodos_matricula.editar',v_institucion)) then
    raise exception 'Permiso denegado.' using errcode='42501'; end if;
  return query select p.id,p.ciclo_id,p.nombre,p.tipo,p.fecha_inicio,p.fecha_fin,p.activo
  from public.periodos_matricula p where p.ciclo_id=p_ciclo_id order by p.fecha_inicio,p.nombre;
end $$;

create or replace function public.rpc_crear_periodo_matricula(p_ciclo_id uuid,p_nombre text,
  p_tipo text,p_fecha_inicio date,p_fecha_fin date)
returns uuid language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare c public.ciclos_escolares%rowtype; v_id uuid; v_contexto uuid;
begin
  select * into c from public.ciclos_escolares where id=p_ciclo_id;
  if not found then raise exception 'El ciclo escolar no existe.' using errcode='P0002'; end if;
  v_contexto:=public.resolver_institucion_operacion(null);
  if c.institucion_id<>v_contexto then raise exception 'El ciclo escolar no pertenece a la institucion actual.' using errcode='P0002'; end if;
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    'configuracion.periodos_matricula.crear',c.institucion_id) then
    raise exception 'Permiso denegado.' using errcode='42501'; end if;
  if btrim(coalesce(p_nombre,''))='' or p_fecha_inicio is null or p_fecha_fin is null
    or p_fecha_inicio>p_fecha_fin then raise exception 'Datos del periodo no validos.' using errcode='22023'; end if;
  if c.fecha_inicio is null or c.fecha_fin is null or p_fecha_inicio<c.fecha_inicio or p_fecha_fin>c.fecha_fin then
    raise exception 'El periodo debe estar dentro de las fechas del ciclo.' using errcode='22023'; end if;
  insert into public.periodos_matricula(ciclo_id,nombre,tipo,fecha_inicio,fecha_fin)
  values(p_ciclo_id,btrim(p_nombre),nullif(btrim(p_tipo),''),p_fecha_inicio,p_fecha_fin) returning id into v_id;
  return v_id;
end $$;

create or replace function public.rpc_actualizar_periodo_matricula(p_periodo_id uuid,p_nombre text,
  p_tipo text,p_fecha_inicio date,p_fecha_fin date,p_activo boolean)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare p public.periodos_matricula%rowtype; c public.ciclos_escolares%rowtype; v_contexto uuid;
begin
  select * into p from public.periodos_matricula where id=p_periodo_id for update;
  if not found then raise exception 'El periodo de matricula no existe.' using errcode='P0002'; end if;
  select * into c from public.ciclos_escolares where id=p.ciclo_id;
  v_contexto:=public.resolver_institucion_operacion(null);
  if c.institucion_id<>v_contexto then raise exception 'El ciclo escolar no pertenece a la institucion actual.' using errcode='P0002'; end if;
  if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(
    case when p.activo and not p_activo then 'configuracion.periodos_matricula.desactivar'
      else 'configuracion.periodos_matricula.editar' end,c.institucion_id) then
    raise exception 'Permiso denegado.' using errcode='42501'; end if;
  if btrim(coalesce(p_nombre,''))='' or p_fecha_inicio is null or p_fecha_fin is null
    or p_fecha_inicio>p_fecha_fin or p_fecha_inicio<c.fecha_inicio or p_fecha_fin>c.fecha_fin then
    raise exception 'El periodo debe tener fechas validas dentro del ciclo.' using errcode='22023'; end if;
  update public.periodos_matricula set nombre=btrim(p_nombre),tipo=nullif(btrim(p_tipo),''),
    fecha_inicio=p_fecha_inicio,fecha_fin=p_fecha_fin,activo=p_activo,updated_at=now()
  where id=p_periodo_id;
end $$;

create or replace function public.rpc_desactivar_periodo_matricula(p_periodo_id uuid)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare p public.periodos_matricula%rowtype;
begin select * into p from public.periodos_matricula where id=p_periodo_id;
  if not found then raise exception 'El periodo de matricula no existe.' using errcode='P0002'; end if;
  perform public.rpc_actualizar_periodo_matricula(p.id,p.nombre,p.tipo,p.fecha_inicio,p.fecha_fin,false);
end $$;

create or replace function public.rpc_reactivar_periodo_matricula(p_periodo_id uuid)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$
declare p public.periodos_matricula%rowtype;
begin select * into p from public.periodos_matricula where id=p_periodo_id;
  if not found then raise exception 'El periodo de matricula no existe.' using errcode='P0002'; end if;
  perform public.rpc_actualizar_periodo_matricula(p.id,p.nombre,p.tipo,p.fecha_inicio,p.fecha_fin,true);
end $$;

revoke execute on function public.resolver_institucion_operacion(uuid) from public,anon,authenticated;
revoke execute on function public.rpc_listar_ciclos_escolares(uuid),
 public.rpc_crear_ciclo_escolar(text,date,date,uuid),
 public.rpc_actualizar_ciclo_escolar(uuid,text,date,date,boolean,text),
 public.rpc_desactivar_ciclo_escolar(uuid,text),public.rpc_reactivar_ciclo_escolar(uuid),
 public.rpc_listar_periodos_matricula(uuid),
 public.rpc_crear_periodo_matricula(uuid,text,text,date,date),
 public.rpc_actualizar_periodo_matricula(uuid,text,text,date,date,boolean),
 public.rpc_desactivar_periodo_matricula(uuid),public.rpc_reactivar_periodo_matricula(uuid)
from public,anon;
grant execute on function public.rpc_listar_ciclos_escolares(uuid),
 public.rpc_crear_ciclo_escolar(text,date,date,uuid),
 public.rpc_actualizar_ciclo_escolar(uuid,text,date,date,boolean,text),
 public.rpc_desactivar_ciclo_escolar(uuid,text),public.rpc_reactivar_ciclo_escolar(uuid),
 public.rpc_listar_periodos_matricula(uuid),
 public.rpc_crear_periodo_matricula(uuid,text,text,date,date),
 public.rpc_actualizar_periodo_matricula(uuid,text,text,date,date,boolean),
 public.rpc_desactivar_periodo_matricula(uuid),public.rpc_reactivar_periodo_matricula(uuid)
to authenticated;
grant execute on all functions in schema public to service_role;

insert into public.schema_migrations(version,nombre,checksum)
values('014','configurar_ciclos_y_periodos_matricula',null) on conflict(version) do nothing;
commit;

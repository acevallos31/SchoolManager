-- Independiza las ventanas de matricula de las fechas academicas del ciclo.
begin;

do $$ begin
  if not exists (select 1 from public.schema_migrations where version = '014') then
    raise exception 'Migracion 015 requiere la migracion 014.';
  end if;
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
  update public.ciclos_escolares set nombre=btrim(p_nombre),fecha_inicio=p_fecha_inicio,
    fecha_fin=p_fecha_fin,activo=p_activo,updated_at=now(),
    fecha_desactivacion=case when p_activo then null when v_activo then now() else fecha_desactivacion end,
    motivo_desactivacion=case when p_activo then null when v_activo then btrim(p_motivo_desactivacion) else motivo_desactivacion end
  where id=p_ciclo_id;
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
  if btrim(coalesce(p_nombre,''))='' or p_fecha_inicio is null or p_fecha_fin is null then
    raise exception 'Nombre y fechas del periodo son obligatorios.' using errcode='22023'; end if;
  if p_fecha_inicio>p_fecha_fin then
    raise exception 'La fecha de inicio del periodo no puede ser posterior a la fecha de fin.' using errcode='22023'; end if;
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
  if btrim(coalesce(p_nombre,''))='' or p_fecha_inicio is null or p_fecha_fin is null then
    raise exception 'Nombre y fechas del periodo son obligatorios.' using errcode='22023'; end if;
  if p_fecha_inicio>p_fecha_fin then
    raise exception 'La fecha de inicio del periodo no puede ser posterior a la fecha de fin.' using errcode='22023'; end if;
  update public.periodos_matricula set nombre=btrim(p_nombre),tipo=nullif(btrim(p_tipo),''),
    fecha_inicio=p_fecha_inicio,fecha_fin=p_fecha_fin,activo=p_activo,updated_at=now()
  where id=p_periodo_id;
end $$;

insert into public.schema_migrations(version,nombre,checksum)
values('015','permitir_periodos_matricula_anticipados',null) on conflict(version) do nothing;
commit;

-- Configuracion segura del catalogo academico previo a matriculas.
begin;
do $$ begin if not exists(select 1 from public.schema_migrations where version='015') then raise exception 'Migracion 016 requiere la migracion 015.'; end if; end $$;

create unique index if not exists ux_grados_nombre_normalizado on public.grados(lower(btrim(nombre)));
create unique index if not exists ux_jornadas_nombre_normalizado on public.jornadas(lower(btrim(nombre)));

-- El nombre de la seccion es unico por institucion, ciclo y grado,
-- independientemente de la jornada.
do $$
begin
  if exists (
    select 1
    from public.secciones s
    group by s.institucion_id,s.ciclo_id,s.grado_id,lower(btrim(s.nombre))
    having count(*)>1
  ) then
    raise exception 'Migracion 016: existen secciones duplicadas por institucion, ciclo, grado y nombre. Corrija los datos antes de continuar.';
  end if;
end
$$;

drop index if exists public.ux_secciones_contexto_jornada_nombre;
drop index if exists public.ux_secciones_contexto_sin_jornada_nombre;

create unique index if not exists ux_secciones_contexto_nombre
  on public.secciones(institucion_id,ciclo_id,grado_id,lower(btrim(nombre)));

insert into public.permisos(codigo,modulo,nombre) values
 ('configuracion.grados.ver','configuracion','Ver grados'),('configuracion.grados.crear','configuracion','Crear grados'),
 ('configuracion.grados.editar','configuracion','Editar grados'),('configuracion.grados.desactivar','configuracion','Desactivar grados'),
 ('configuracion.jornadas.ver','configuracion','Ver jornadas'),('configuracion.jornadas.crear','configuracion','Crear jornadas'),
 ('configuracion.jornadas.editar','configuracion','Editar jornadas'),('configuracion.jornadas.desactivar','configuracion','Desactivar jornadas'),
 ('configuracion.secciones.ver','configuracion','Ver secciones'),('configuracion.secciones.crear','configuracion','Crear secciones'),
 ('configuracion.secciones.editar','configuracion','Editar secciones'),('configuracion.secciones.desactivar','configuracion','Desactivar secciones')
on conflict(codigo) do nothing;
insert into public.roles_permisos(rol_id,permiso_id)
select r.id,p.id from public.roles r cross join public.permisos p where r.codigo='admin'
 and (p.codigo like 'configuracion.grados.%' or p.codigo like 'configuracion.jornadas.%' or p.codigo like 'configuracion.secciones.%')
on conflict do nothing;

create or replace function public.rpc_listar_grados(p_institucion_id uuid default null)
returns table(id uuid,nombre text,orden integer,activo boolean) language plpgsql security definer
set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.grados.ver',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 return query select g.id,g.nombre,g.orden,g.activo from public.grados g order by g.orden,g.nombre;
end $$;
create or replace function public.rpc_crear_grado(p_nombre text,p_orden integer,p_institucion_id uuid default null)
returns uuid language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; x uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.grados.crear',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 if btrim(coalesce(p_nombre,''))='' then raise exception 'El nombre del grado es obligatorio.' using errcode='22023'; end if;
 if p_orden is null or p_orden<0 then raise exception 'El orden del grado debe ser cero o mayor.' using errcode='22023'; end if;
 insert into public.grados(nombre,orden) values(btrim(p_nombre),p_orden) returning id into x; return x;
end $$;
create or replace function public.rpc_actualizar_grado(p_grado_id uuid,p_nombre text,p_orden integer,p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.grados.editar',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 if btrim(coalesce(p_nombre,''))='' or p_orden is null or p_orden<0 then raise exception 'Nombre y orden del grado no son validos.' using errcode='22023'; end if;
 update public.grados set nombre=btrim(p_nombre),orden=p_orden,updated_at=now() where id=p_grado_id;
 if not found then raise exception 'El grado no existe.' using errcode='P0002'; end if;
end $$;
create or replace function public.rpc_cambiar_estado_grado(p_grado_id uuid,p_activo boolean,p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(case when p_activo then 'configuracion.grados.editar' else 'configuracion.grados.desactivar' end,v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 update public.grados set activo=p_activo,updated_at=now() where id=p_grado_id;
 if not found then raise exception 'El grado no existe.' using errcode='P0002'; end if;
end $$;
create or replace function public.rpc_desactivar_grado(p_grado_id uuid,p_institucion_id uuid default null) returns void language sql security definer set search_path=pg_catalog,public,pg_temp as $$select public.rpc_cambiar_estado_grado(p_grado_id,false,p_institucion_id)$$;
create or replace function public.rpc_reactivar_grado(p_grado_id uuid,p_institucion_id uuid default null) returns void language sql security definer set search_path=pg_catalog,public,pg_temp as $$select public.rpc_cambiar_estado_grado(p_grado_id,true,p_institucion_id)$$;

create or replace function public.rpc_listar_jornadas(p_institucion_id uuid default null)
returns table(id uuid,nombre text,activo boolean) language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id); if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.jornadas.ver',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 return query select j.id,j.nombre,j.activo from public.jornadas j order by j.nombre;
end $$;
create or replace function public.rpc_crear_jornada(p_nombre text,p_institucion_id uuid default null)
returns uuid language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid;x uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id); if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.jornadas.crear',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 if btrim(coalesce(p_nombre,''))='' then raise exception 'El nombre de la jornada es obligatorio.' using errcode='22023'; end if;
 insert into public.jornadas(nombre) values(btrim(p_nombre)) returning id into x;return x;
end $$;
create or replace function public.rpc_actualizar_jornada(p_jornada_id uuid,p_nombre text,p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.jornadas.editar',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 if btrim(coalesce(p_nombre,''))='' then raise exception 'El nombre de la jornada es obligatorio.' using errcode='22023'; end if;
 update public.jornadas set nombre=btrim(p_nombre),updated_at=now() where id=p_jornada_id;if not found then raise exception 'La jornada no existe.' using errcode='P0002';end if;
end $$;
create or replace function public.rpc_cambiar_estado_jornada(p_jornada_id uuid,p_activo boolean,p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(case when p_activo then 'configuracion.jornadas.editar' else 'configuracion.jornadas.desactivar' end,v) then raise exception 'Permiso denegado.' using errcode='42501';end if;
 update public.jornadas set activo=p_activo,updated_at=now() where id=p_jornada_id;if not found then raise exception 'La jornada no existe.' using errcode='P0002';end if;
end $$;
create or replace function public.rpc_desactivar_jornada(p_jornada_id uuid,p_institucion_id uuid default null) returns void language sql security definer set search_path=pg_catalog,public,pg_temp as $$select public.rpc_cambiar_estado_jornada(p_jornada_id,false,p_institucion_id)$$;
create or replace function public.rpc_reactivar_jornada(p_jornada_id uuid,p_institucion_id uuid default null) returns void language sql security definer set search_path=pg_catalog,public,pg_temp as $$select public.rpc_cambiar_estado_jornada(p_jornada_id,true,p_institucion_id)$$;

create or replace function public.rpc_listar_secciones(p_ciclo_id uuid,p_institucion_id uuid default null)
returns table(id uuid,institucion_id uuid,ciclo_id uuid,grado_id uuid,grado_nombre text,jornada_id uuid,jornada_nombre text,nombre text,cupo integer,activo boolean,fecha_desactivacion timestamptz,motivo_desactivacion text)
language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$declare v uuid;begin
 v:=public.resolver_institucion_operacion(p_institucion_id);if not exists(select 1 from public.ciclos_escolares ce where ce.id=p_ciclo_id and ce.institucion_id=v) then raise exception 'El ciclo no pertenece a la institucion actual.' using errcode='P0002';end if;
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.secciones.ver',v) then raise exception 'Permiso denegado.' using errcode='42501';end if;
 return query select s.id,s.institucion_id,s.ciclo_id,s.grado_id,g.nombre,s.jornada_id,j.nombre,s.nombre,s.cupo,s.activo,s.fecha_desactivacion,s.motivo_desactivacion from public.secciones s join public.grados g on g.id=s.grado_id left join public.jornadas j on j.id=s.jornada_id where s.ciclo_id=p_ciclo_id and s.institucion_id=v order by g.orden,g.nombre,j.nombre nulls first,s.nombre;
end $$;
create or replace function public.rpc_crear_seccion(p_institucion_id uuid,p_ciclo_id uuid,p_grado_id uuid,p_jornada_id uuid,p_nombre text,p_cupo integer default null)
returns uuid language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$declare v uuid;x uuid;begin
 v:=public.resolver_institucion_operacion(p_institucion_id);if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.secciones.crear',v) then raise exception 'Permiso denegado.' using errcode='42501';end if;
 if not exists(select 1 from public.ciclos_escolares where id=p_ciclo_id and institucion_id=v and activo) then raise exception 'El ciclo no pertenece a la institucion actual o esta inactivo.' using errcode='23503';end if;
 if not exists(select 1 from public.grados where id=p_grado_id and activo) then raise exception 'El grado no existe o esta inactivo.' using errcode='23503';end if;
 if p_jornada_id is not null and not exists(select 1 from public.jornadas where id=p_jornada_id and activo) then raise exception 'La jornada no existe o esta inactiva.' using errcode='23503';end if;
 if btrim(coalesce(p_nombre,''))='' then raise exception 'El nombre de la seccion es obligatorio.' using errcode='22023';end if;if p_cupo is not null and p_cupo<=0 then raise exception 'El cupo debe ser mayor que cero.' using errcode='22023';end if;
 insert into public.secciones(institucion_id,ciclo_id,grado_id,jornada_id,nombre,cupo)values(v,p_ciclo_id,p_grado_id,p_jornada_id,btrim(p_nombre),p_cupo)returning id into x;return x;
end $$;

-- Las firmas anteriores permitirian omitir el contexto en modo multi.
drop function if exists public.rpc_actualizar_seccion(uuid,uuid,uuid,uuid,text,integer);
drop function if exists public.rpc_desactivar_seccion(uuid,text);
drop function if exists public.rpc_reactivar_seccion(uuid);

create or replace function public.rpc_actualizar_seccion(p_seccion_id uuid,p_ciclo_id uuid,p_grado_id uuid,p_jornada_id uuid,p_nombre text,p_cupo integer,p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$declare s public.secciones%rowtype;v uuid;begin
 select * into s from public.secciones where id=p_seccion_id for update;if not found then raise exception 'La seccion no existe.' using errcode='P0002';end if;
 v:=public.resolver_institucion_operacion(p_institucion_id);if s.institucion_id<>v then raise exception 'La seccion no pertenece a la institucion actual.' using errcode='P0002';end if;
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.secciones.editar',v) then raise exception 'Permiso denegado.' using errcode='42501';end if;
 if exists(select 1 from public.matriculas where seccion_id=s.id) and (p_ciclo_id is distinct from s.ciclo_id or p_grado_id is distinct from s.grado_id or p_jornada_id is distinct from s.jornada_id) then raise exception 'Una seccion con matriculas no puede cambiar de ciclo, grado ni jornada.' using errcode='23514';end if;
 if not exists(select 1 from public.ciclos_escolares where id=p_ciclo_id and institucion_id=v and activo) then raise exception 'El ciclo no pertenece a la institucion actual o esta inactivo.' using errcode='23503';end if;
 if not exists(select 1 from public.grados where id=p_grado_id and activo) then raise exception 'El grado no existe o esta inactivo.' using errcode='23503';end if;
 if p_jornada_id is not null and not exists(select 1 from public.jornadas where id=p_jornada_id and activo) then raise exception 'La jornada no existe o esta inactiva.' using errcode='23503';end if;
 if btrim(coalesce(p_nombre,''))='' or (p_cupo is not null and p_cupo<=0) then raise exception 'Nombre o cupo de seccion no valido.' using errcode='22023';end if;
 update public.secciones set ciclo_id=p_ciclo_id,grado_id=p_grado_id,jornada_id=p_jornada_id,nombre=btrim(p_nombre),cupo=p_cupo,updated_at=now() where id=s.id;
end $$;
create or replace function public.rpc_desactivar_seccion(p_seccion_id uuid,p_motivo text,p_institucion_id uuid default null) returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$declare s public.secciones%rowtype;v uuid;begin
 select * into s from public.secciones where id=p_seccion_id for update;if not found then raise exception 'La seccion no existe.' using errcode='P0002';end if;v:=public.resolver_institucion_operacion(p_institucion_id);if s.institucion_id<>v then raise exception 'La seccion no pertenece a la institucion actual.' using errcode='P0002';end if;
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.secciones.desactivar',v) then raise exception 'Permiso denegado.' using errcode='42501';end if;if btrim(coalesce(p_motivo,''))='' then raise exception 'El motivo es obligatorio.' using errcode='22023';end if;
 update public.secciones set activo=false,fecha_desactivacion=now(),motivo_desactivacion=btrim(p_motivo),updated_at=now() where id=s.id;
end $$;
create or replace function public.rpc_reactivar_seccion(p_seccion_id uuid,p_institucion_id uuid default null) returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$declare s public.secciones%rowtype;v uuid;begin
 select * into s from public.secciones where id=p_seccion_id for update;if not found then raise exception 'La seccion no existe.' using errcode='P0002';end if;v:=public.resolver_institucion_operacion(p_institucion_id);if s.institucion_id<>v then raise exception 'La seccion no pertenece a la institucion actual.' using errcode='P0002';end if;
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.secciones.editar',v) then raise exception 'Permiso denegado.' using errcode='42501';end if;
 if not exists(select 1 from public.ciclos_escolares where id=s.ciclo_id and institucion_id=v and activo) or not exists(select 1 from public.grados where id=s.grado_id and activo) or (s.jornada_id is not null and not exists(select 1 from public.jornadas where id=s.jornada_id and activo)) then raise exception 'La estructura academica de la seccion no existe o esta inactiva.' using errcode='23503';end if;
 update public.secciones set activo=true,fecha_desactivacion=null,motivo_desactivacion=null,updated_at=now() where id=s.id;
end $$;

revoke execute on function public.rpc_listar_grados(uuid),public.rpc_crear_grado(text,integer,uuid),public.rpc_actualizar_grado(uuid,text,integer,uuid),public.rpc_cambiar_estado_grado(uuid,boolean,uuid),public.rpc_desactivar_grado(uuid,uuid),public.rpc_reactivar_grado(uuid,uuid),public.rpc_listar_jornadas(uuid),public.rpc_crear_jornada(text,uuid),public.rpc_actualizar_jornada(uuid,text,uuid),public.rpc_cambiar_estado_jornada(uuid,boolean,uuid),public.rpc_desactivar_jornada(uuid,uuid),public.rpc_reactivar_jornada(uuid,uuid),public.rpc_listar_secciones(uuid,uuid),public.rpc_crear_seccion(uuid,uuid,uuid,uuid,text,integer),public.rpc_actualizar_seccion(uuid,uuid,uuid,uuid,text,integer,uuid),public.rpc_desactivar_seccion(uuid,text,uuid),public.rpc_reactivar_seccion(uuid,uuid) from public,anon;
grant execute on function public.rpc_listar_grados(uuid),public.rpc_crear_grado(text,integer,uuid),public.rpc_actualizar_grado(uuid,text,integer,uuid),public.rpc_desactivar_grado(uuid,uuid),public.rpc_reactivar_grado(uuid,uuid),public.rpc_listar_jornadas(uuid),public.rpc_crear_jornada(text,uuid),public.rpc_actualizar_jornada(uuid,text,uuid),public.rpc_desactivar_jornada(uuid,uuid),public.rpc_reactivar_jornada(uuid,uuid),public.rpc_listar_secciones(uuid,uuid),public.rpc_crear_seccion(uuid,uuid,uuid,uuid,text,integer),public.rpc_actualizar_seccion(uuid,uuid,uuid,uuid,text,integer,uuid),public.rpc_desactivar_seccion(uuid,text,uuid),public.rpc_reactivar_seccion(uuid,uuid) to authenticated;
grant execute on all functions in schema public to service_role;
insert into public.schema_migrations(version,nombre,checksum)values('016','configurar_grados_jornadas_secciones',null)on conflict(version)do nothing;
commit;

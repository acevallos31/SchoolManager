-- Migracion 020: grados y jornadas pasan a catalogo por institucion.
--
-- Cierra la deuda #2 (docs/technical-debt.md): grados/jornadas eran tablas
-- GLOBALES (sin institucion_id) referenciadas por secciones (per-institucion).
-- Sus RPC resolvian el permiso con la institucion pero NO aislan las filas:
--   - rpc_listar_grados/jornadas devolvia TODOS los registros de TODAS las
--     instituciones (fuga de listado).
--   - rpc_crear_grado/jornada insertaba sin contexto (global).
--   - rpc_actualizar_grado/jornada y rpc_cambiar_estado_grado/jornada
--     actualizaban por id sin comprobar pertenencia (editar/desactivar el
--     catalogo de OTRA institucion por UUID conocido).
--   - RLS de grados/jornadas usaba usuario_tiene_permiso_en_algun_ambito
--     (cualquier institucion) en lugar de scoping por fila.
--
-- Solucion (decision documentada): institucion_id DIRECTO en grados y
-- jornadas, el patron canonico per-institucion del resto del esquema
-- (secciones, ciclos_escolares, conceptos_financieros, planes_pago,
-- responsables). Sin tabla puente: no hay entidad intermedia canonica.
-- Backfill determinista (sin duplicar datos silenciosamente): asigna por
-- referencia real desde secciones; ante un grado compartido por varias
-- instituciones o un huerfano en modo multi, FALLA con error explicito en
-- lugar de inventar contexto (mismo criterio que la migracion 008).
begin;

-- Precedencia: requiere 019.
do $$ begin if not exists(select 1 from public.schema_migrations where version='019') then
  raise exception 'Migracion 020 requiere la migracion 019 (cargos).';
end if; end $$;

-- ============ 1. COLUMNA INSTITUCION (nullable primero) ============
alter table public.grados  add column if not exists institucion_id uuid null;
alter table public.jornadas add column if not exists institucion_id uuid null;

-- ============ 2. BACKFILL DETERMINISTA ============
do $$
declare v_insts int;
begin
  select count(*) into v_insts
    from public.instituciones where activo;

  if v_insts = 1 then
    -- Caso monoinstitucional: una unica institucion, sin ambiguedad.
    update public.grados
       set institucion_id = (select id from public.instituciones where activo limit 1)
     where institucion_id is null;
    update public.jornadas
       set institucion_id = (select id from public.instituciones where activo limit 1)
     where institucion_id is null;
  elsif v_insts > 1 then
    -- Grados: asignar por referencia inequivoca desde secciones (una sola
    -- institucion usa ese grado). Si un grado es compartido por varias
    -- instituciones o es huerfano sin contexto deducible: abortar.
    update public.grados g
       set institucion_id = x.institucion_id
      from (
        select s.grado_id as grado_id, min(s.institucion_id) as institucion_id
          from public.secciones s
         group by s.grado_id
        having count(distinct s.institucion_id) = 1
      ) x
     where g.id = x.grado_id and g.institucion_id is null;

    if exists (
      select 1 from public.grados g
       where g.institucion_id is null
         and exists (
           select 1 from public.secciones s
            where s.grado_id = g.id
           group by s.grado_id
          having count(distinct s.institucion_id) > 1
         )
    ) then
      raise exception 'Migracion 020: hay grados compartidos por varias instituciones. Resuelva manualmente su contexto antes de migrar; no se duplican en silencio.';
    end if;
    if exists (
      select 1 from public.grados g
       where g.institucion_id is null
         and not exists (select 1 from public.secciones s where s.grado_id = g.id)
    ) then
      raise exception 'Migracion 020: hay grados huerfanos (sin secciones) sin contexto deducible en modo multi-institucion. Resuelvalos manualmente.';
    end if;

    -- Jornadas: equivalente.
    update public.jornadas j
       set institucion_id = x.institucion_id
      from (
        select s.jornada_id as jornada_id, min(s.institucion_id) as institucion_id
          from public.secciones s
         where s.jornada_id is not null
         group by s.jornada_id
        having count(distinct s.institucion_id) = 1
      ) x
     where j.id = x.jornada_id and j.institucion_id is null;

    if exists (
      select 1 from public.jornadas j
       where j.institucion_id is null
         and exists (
           select 1 from public.secciones s
            where s.jornada_id = j.id
           group by s.jornada_id
          having count(distinct s.institucion_id) > 1
         )
    ) then
      raise exception 'Migracion 020: hay jornadas compartidas por varias instituciones. Resuelva manualmente su contexto antes de migrar; no se duplican en silencio.';
    end if;
    if exists (
      select 1 from public.jornadas j
       where j.institucion_id is null
         and not exists (select 1 from public.secciones s where s.jornada_id = j.id)
    ) then
      raise exception 'Migracion 020: hay jornadas huerfanas (sin secciones) sin contexto deducible en modo multi-institucion. Resuelvalas manualmente.';
    end if;
  end if;

  -- Invariancia: nada debe quedar sin contexto tras el backfill.
  if exists (select 1 from public.grados where institucion_id is null)
     or exists (select 1 from public.jornadas where institucion_id is null) then
    raise exception 'Migracion 020: quedaron grados o jornadas sin institucion asignada tras el backfill.';
  end if;
end $$;

-- ============ 3. NOT NULL (post-backfill) + FK + indice ============
alter table public.grados  alter column institucion_id set not null;
alter table public.jornadas alter column institucion_id set not null;

alter table public.grados
  add constraint fk_grados_institucion
  foreign key (institucion_id) references public.instituciones(id) on delete restrict;
alter table public.jornadas
  add constraint fk_jornadas_institucion
  foreign key (institucion_id) references public.instituciones(id) on delete restrict;

create index if not exists ix_grados_institucion_id on public.grados(institucion_id);
create index if not exists ix_jornadas_institucion_id on public.jornadas(institucion_id);

-- ============ 4. UNICIDAD POR INSTITUCION (reemplaza la global) ============
-- La unicidad global pre-020 era el constraint UNIQUE(nombre) (auto-nombrado
-- *_nombre_key por la baseline). Se retira: nombres iguales pueden convivir
-- entre instituciones y la unicidad queda por-institucion. Se elimina tambien
-- (defensivo) el indice ux_*_nombre_normalizado por si alguna instalacion lo
-- hubiera creado con ese nombre historico.
alter table public.grados drop constraint if exists grados_nombre_key;
alter table public.jornadas drop constraint if exists jornadas_nombre_key;
drop index if exists public.ux_grados_nombre_normalizado;
drop index if exists public.ux_jornadas_nombre_normalizado;

create unique index if not exists ux_grados_institucion_nombre
  on public.grados(institucion_id, lower(btrim(nombre)));
create unique index if not exists ux_jornadas_institucion_nombre
  on public.jornadas(institucion_id, lower(btrim(nombre)));

-- ============ 5. SECCIONES: FK COMPUESTA de contexto (misma institucion) ============
create unique index if not exists ux_grados_id_institucion on public.grados(id, institucion_id);
create unique index if not exists ux_jornadas_id_institucion on public.jornadas(id, institucion_id);

alter table public.secciones drop constraint if exists fk_secciones_grado;
alter table public.secciones drop constraint if exists fk_secciones_jornada;
alter table public.secciones
  add constraint fk_secciones_grado_contexto
  foreign key (grado_id, institucion_id)
  references public.grados(id, institucion_id) on delete restrict;
alter table public.secciones
  add constraint fk_secciones_jornada_contexto
  foreign key (jornada_id, institucion_id)
  references public.jornadas(id, institucion_id) on delete restrict;

-- ============ 6. RLS: SCOPING POR FILA (institucion) ============
drop policy if exists grados_select on public.grados;
create policy grados_select on public.grados for select to authenticated using (
  public.usuario_tiene_permiso_actual('configuracion.grados.ver', institucion_id)
  or public.usuario_tiene_permiso_actual('academico.secciones.ver', institucion_id)
  or public.usuario_tiene_permiso_actual('academico.matriculas.ver', institucion_id)
);
drop policy if exists jornadas_select on public.jornadas;
create policy jornadas_select on public.jornadas for select to authenticated using (
  public.usuario_tiene_permiso_actual('configuracion.jornadas.ver', institucion_id)
  or public.usuario_tiene_permiso_actual('academico.secciones.ver', institucion_id)
  or public.usuario_tiene_permiso_actual('academico.matriculas.ver', institucion_id)
);

-- ============ 7. RPC GRADOS: aislar por institucion ============
-- Cambia la fila de retorno (se anade institucion_id): hay que DROP antes.
drop function if exists public.rpc_listar_grados(uuid);
create or replace function public.rpc_listar_grados(p_institucion_id uuid default null)
returns table(id uuid,nombre text,orden integer,activo boolean,institucion_id uuid)
language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.grados.ver',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 return query select g.id,g.nombre,g.orden,g.activo,g.institucion_id from public.grados g where g.institucion_id=v order by g.orden,g.nombre;
end $$;
create or replace function public.rpc_crear_grado(p_nombre text,p_orden integer,p_institucion_id uuid default null)
returns uuid language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; x uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.grados.crear',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 if btrim(coalesce(p_nombre,''))='' then raise exception 'El nombre del grado es obligatorio.' using errcode='22023'; end if;
 if p_orden is null or p_orden<0 then raise exception 'El orden del grado debe ser cero o mayor.' using errcode='22023'; end if;
 insert into public.grados(nombre,orden,institucion_id) values(btrim(p_nombre),p_orden,v) returning id into x; return x;
end $$;
create or replace function public.rpc_actualizar_grado(p_grado_id uuid,p_nombre text,p_orden integer,p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.grados.editar',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 if btrim(coalesce(p_nombre,''))='' or p_orden is null or p_orden<0 then raise exception 'Nombre y orden del grado no son validos.' using errcode='22023'; end if;
 update public.grados set nombre=btrim(p_nombre),orden=p_orden,updated_at=now() where id=p_grado_id and institucion_id=v;
 if not found then raise exception 'El grado no existe.' using errcode='P0002'; end if;
end $$;
create or replace function public.rpc_cambiar_estado_grado(p_grado_id uuid,p_activo boolean,p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(case when p_activo then 'configuracion.grados.editar' else 'configuracion.grados.desactivar' end,v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 update public.grados set activo=p_activo,updated_at=now() where id=p_grado_id and institucion_id=v;
 if not found then raise exception 'El grado no existe.' using errcode='P0002'; end if;
end $$;
create or replace function public.rpc_desactivar_grado(p_grado_id uuid,p_institucion_id uuid default null) returns void language sql security definer set search_path=pg_catalog,public,pg_temp as $$select public.rpc_cambiar_estado_grado(p_grado_id,false,p_institucion_id)$$;
create or replace function public.rpc_reactivar_grado(p_grado_id uuid,p_institucion_id uuid default null) returns void language sql security definer set search_path=pg_catalog,public,pg_temp as $$select public.rpc_cambiar_estado_grado(p_grado_id,true,p_institucion_id)$$;

-- ============ 8. RPC JORNADAS: aislar por institucion ============
drop function if exists public.rpc_listar_jornadas(uuid);
create or replace function public.rpc_listar_jornadas(p_institucion_id uuid default null)
returns table(id uuid,nombre text,activo boolean,institucion_id uuid)
language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.jornadas.ver',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 return query select j.id,j.nombre,j.activo,j.institucion_id from public.jornadas j where j.institucion_id=v order by j.nombre;
end $$;
create or replace function public.rpc_crear_jornada(p_nombre text,p_institucion_id uuid default null)
returns uuid language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid;x uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.jornadas.crear',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 if btrim(coalesce(p_nombre,''))='' then raise exception 'El nombre de la jornada es obligatorio.' using errcode='22023'; end if;
 insert into public.jornadas(nombre,institucion_id) values(btrim(p_nombre),v) returning id into x;return x;
end $$;
create or replace function public.rpc_actualizar_jornada(p_jornada_id uuid,p_nombre text,p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.jornadas.editar',v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 if btrim(coalesce(p_nombre,''))='' then raise exception 'El nombre de la jornada es obligatorio.' using errcode='22023'; end if;
 update public.jornadas set nombre=btrim(p_nombre),updated_at=now() where id=p_jornada_id and institucion_id=v;
 if not found then raise exception 'La jornada no existe.' using errcode='P0002'; end if;
end $$;
create or replace function public.rpc_cambiar_estado_jornada(p_jornada_id uuid,p_activo boolean,p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$ declare v uuid; begin
 v:=public.resolver_institucion_operacion(p_institucion_id);
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual(case when p_activo then 'configuracion.jornadas.editar' else 'configuracion.jornadas.desactivar' end,v) then raise exception 'Permiso denegado.' using errcode='42501'; end if;
 update public.jornadas set activo=p_activo,updated_at=now() where id=p_jornada_id and institucion_id=v;
 if not found then raise exception 'La jornada no existe.' using errcode='P0002'; end if;
end $$;
create or replace function public.rpc_desactivar_jornada(p_jornada_id uuid,p_institucion_id uuid default null) returns void language sql security definer set search_path=pg_catalog,public,pg_temp as $$select public.rpc_cambiar_estado_jornada(p_jornada_id,false,p_institucion_id)$$;
create or replace function public.rpc_reactivar_jornada(p_jornada_id uuid,p_institucion_id uuid default null) returns void language sql security definer set search_path=pg_catalog,public,pg_temp as $$select public.rpc_cambiar_estado_jornada(p_jornada_id,true,p_institucion_id)$$;

-- ============ 9. RPC SECCIONES: validar contexto institucional ============
create or replace function public.rpc_listar_secciones(p_ciclo_id uuid,p_institucion_id uuid default null)
returns table(id uuid,institucion_id uuid,ciclo_id uuid,grado_id uuid,grado_nombre text,jornada_id uuid,jornada_nombre text,nombre text,cupo integer,activo boolean,fecha_desactivacion timestamptz,motivo_desactivacion text)
language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$declare v uuid;begin
 v:=public.resolver_institucion_operacion(p_institucion_id);if not exists(select 1 from public.ciclos_escolares ce where ce.id=p_ciclo_id and ce.institucion_id=v) then raise exception 'El ciclo no pertenece a la institucion actual.' using errcode='P0002';end if;
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.secciones.ver',v) then raise exception 'Permiso denegado.' using errcode='42501';end if;
 return query select s.id,s.institucion_id,s.ciclo_id,s.grado_id,g.nombre,s.jornada_id,j.nombre,s.nombre,s.cupo,s.activo,s.fecha_desactivacion,s.motivo_desactivacion from public.secciones s join public.grados g on g.id=s.grado_id and g.institucion_id=v left join public.jornadas j on j.id=s.jornada_id and j.institucion_id=v where s.ciclo_id=p_ciclo_id and s.institucion_id=v order by g.orden,g.nombre,j.nombre nulls first,s.nombre;
end $$;
create or replace function public.rpc_crear_seccion(p_institucion_id uuid,p_ciclo_id uuid,p_grado_id uuid,p_jornada_id uuid,p_nombre text,p_cupo integer default null)
returns uuid language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$declare v uuid;x uuid;begin
 v:=public.resolver_institucion_operacion(p_institucion_id);if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.secciones.crear',v) then raise exception 'Permiso denegado.' using errcode='42501';end if;
 if not exists(select 1 from public.ciclos_escolares where id=p_ciclo_id and institucion_id=v and activo) then raise exception 'El ciclo no pertenece a la institucion actual o esta inactivo.' using errcode='23503';end if;
 if not exists(select 1 from public.grados where id=p_grado_id and institucion_id=v and activo) then raise exception 'El grado no existe, pertenece a otra institucion o esta inactivo.' using errcode='23503';end if;
 if p_jornada_id is not null and not exists(select 1 from public.jornadas where id=p_jornada_id and institucion_id=v and activo) then raise exception 'La jornada no existe, pertenece a otra institucion o esta inactiva.' using errcode='23503';end if;
 if btrim(coalesce(p_nombre,''))='' then raise exception 'El nombre de la seccion es obligatorio.' using errcode='22023';end if;if p_cupo is not null and p_cupo<=0 then raise exception 'El cupo debe ser mayor que cero.' using errcode='22023';end if;
 insert into public.secciones(institucion_id,ciclo_id,grado_id,jornada_id,nombre,cupo)values(v,p_ciclo_id,p_grado_id,p_jornada_id,btrim(p_nombre),p_cupo)returning id into x;return x;
end $$;
create or replace function public.rpc_actualizar_seccion(p_seccion_id uuid,p_ciclo_id uuid,p_grado_id uuid,p_jornada_id uuid,p_nombre text,p_cupo integer,p_institucion_id uuid default null)
returns void language plpgsql security definer set search_path=pg_catalog,public,pg_temp as $$declare s public.secciones%rowtype;v uuid;begin
 select * into s from public.secciones where id=p_seccion_id for update;if not found then raise exception 'La seccion no existe.' using errcode='P0002';end if;
 v:=public.resolver_institucion_operacion(p_institucion_id);if s.institucion_id<>v then raise exception 'La seccion no pertenece a la institucion actual.' using errcode='P0002';end if;
 if public.usuario_actual_id() is null or not public.usuario_tiene_permiso_actual('configuracion.secciones.editar',v) then raise exception 'Permiso denegado.' using errcode='42501';end if;
 if exists(select 1 from public.matriculas where seccion_id=s.id) and (p_ciclo_id is distinct from s.ciclo_id or p_grado_id is distinct from s.grado_id or p_jornada_id is distinct from s.jornada_id) then raise exception 'Una seccion con matriculas no puede cambiar de ciclo, grado ni jornada.' using errcode='23514';end if;
 if not exists(select 1 from public.ciclos_escolares where id=p_ciclo_id and institucion_id=v and activo) then raise exception 'El ciclo no pertenece a la institucion actual o esta inactivo.' using errcode='23503';end if;
 if not exists(select 1 from public.grados where id=p_grado_id and institucion_id=v and activo) then raise exception 'El grado no existe, pertenece a otra institucion o esta inactivo.' using errcode='23503';end if;
 if p_jornada_id is not null and not exists(select 1 from public.jornadas where id=p_jornada_id and institucion_id=v and activo) then raise exception 'La jornada no existe, pertenece a otra institucion o esta inactiva.' using errcode='23503';end if;
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
 if not exists(select 1 from public.ciclos_escolares where id=s.ciclo_id and institucion_id=v and activo) or not exists(select 1 from public.grados where id=s.grado_id and institucion_id=v and activo) or (s.jornada_id is not null and not exists(select 1 from public.jornadas where id=s.jornada_id and institucion_id=v and activo)) then raise exception 'La estructura academica de la seccion no existe o esta inactiva.' using errcode='23503';end if;
 update public.secciones set activo=true,fecha_desactivacion=null,motivo_desactivacion=null,updated_at=now() where id=s.id;
end $$;

-- ============ 10. GRANTS (mismas firmas; re-assert como los siblings) ============
revoke execute on function public.rpc_listar_grados(uuid),public.rpc_crear_grado(text,integer,uuid),public.rpc_actualizar_grado(uuid,text,integer,uuid),public.rpc_cambiar_estado_grado(uuid,boolean,uuid),public.rpc_desactivar_grado(uuid,uuid),public.rpc_reactivar_grado(uuid,uuid),public.rpc_listar_jornadas(uuid),public.rpc_crear_jornada(text,uuid),public.rpc_actualizar_jornada(uuid,text,uuid),public.rpc_cambiar_estado_jornada(uuid,boolean,uuid),public.rpc_desactivar_jornada(uuid,uuid),public.rpc_reactivar_jornada(uuid,uuid),public.rpc_listar_secciones(uuid,uuid),public.rpc_crear_seccion(uuid,uuid,uuid,uuid,text,integer),public.rpc_actualizar_seccion(uuid,uuid,uuid,uuid,text,integer,uuid),public.rpc_desactivar_seccion(uuid,text,uuid),public.rpc_reactivar_seccion(uuid,uuid) from public,anon;
grant execute on function public.rpc_listar_grados(uuid),public.rpc_crear_grado(text,integer,uuid),public.rpc_actualizar_grado(uuid,text,integer,uuid),public.rpc_cambiar_estado_grado(uuid,boolean,uuid),public.rpc_desactivar_grado(uuid,uuid),public.rpc_reactivar_grado(uuid,uuid),public.rpc_listar_jornadas(uuid),public.rpc_crear_jornada(text,uuid),public.rpc_actualizar_jornada(uuid,text,uuid),public.rpc_cambiar_estado_jornada(uuid,boolean,uuid),public.rpc_desactivar_jornada(uuid,uuid),public.rpc_reactivar_jornada(uuid,uuid),public.rpc_listar_secciones(uuid,uuid),public.rpc_crear_seccion(uuid,uuid,uuid,uuid,text,integer),public.rpc_actualizar_seccion(uuid,uuid,uuid,uuid,text,integer,uuid),public.rpc_desactivar_seccion(uuid,text,uuid),public.rpc_reactivar_seccion(uuid,uuid) to authenticated;
grant execute on all functions in schema public to service_role;

insert into public.schema_migrations(version,nombre,checksum)values('020','grados_jornadas_multiinstitucion',null)on conflict(version)do nothing;
commit;

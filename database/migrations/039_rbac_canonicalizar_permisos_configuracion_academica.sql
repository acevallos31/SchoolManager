-- Migracion 039 - puente canonico de permisos para configuracion academica.
--
-- Las RPC historicas de ciclos/periodos y estructura academica conservan
-- checks internos configuracion.*. Desde 023/024 la aplicacion autoriza con
-- academico.ciclos.* y academico.estructura.*. Los roles institucionales
-- dinamicos no deben recibir aliases internos ocultos/no delegables solo para
-- atravesar esa segunda capa.
--
-- Esta migracion centraliza la compatibilidad en usuario_tiene_permiso_actual:
-- un alias interno se satisface con el permiso legacy exacto O con su permiso
-- canonico equivalente. No cambia firmas de RPC ni relaja el scope de
-- institucion que recibe el helper.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '038') then
    raise exception 'La migracion 039 requiere la 038 aplicada previamente.';
  end if;
end
$$;

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
  select
    coalesce(public.usuario_tiene_permiso(
      auth.uid(), p_permiso_codigo, p_institucion_id
    ), false)
    or
    coalesce(public.usuario_tiene_permiso(
      auth.uid(),
      case
        when p_permiso_codigo in (
          'configuracion.ciclos.ver',
          'configuracion.periodos_matricula.ver'
        ) then 'academico.ciclos.ver'
        when p_permiso_codigo in (
          'configuracion.ciclos.crear',
          'configuracion.periodos_matricula.crear'
        ) then 'academico.ciclos.crear'
        when p_permiso_codigo in (
          'configuracion.ciclos.editar',
          'configuracion.periodos_matricula.editar'
        ) then 'academico.ciclos.editar'
        when p_permiso_codigo in (
          'configuracion.ciclos.desactivar',
          'configuracion.periodos_matricula.desactivar'
        ) then 'academico.ciclos.desactivar'
        when p_permiso_codigo in (
          'configuracion.grados.ver',
          'configuracion.jornadas.ver',
          'configuracion.secciones.ver'
        ) then 'academico.estructura.ver'
        when p_permiso_codigo in (
          'configuracion.grados.crear',
          'configuracion.grados.editar',
          'configuracion.jornadas.crear',
          'configuracion.jornadas.editar',
          'configuracion.secciones.crear',
          'configuracion.secciones.editar'
        ) then 'academico.estructura.editar'
        when p_permiso_codigo in (
          'configuracion.grados.desactivar',
          'configuracion.jornadas.desactivar',
          'configuracion.secciones.desactivar'
        ) then 'academico.estructura.desactivar'
        else p_permiso_codigo
      end,
      p_institucion_id
    ), false);
$$;

insert into public.schema_migrations(version, nombre, checksum)
values ('039', 'rbac_canonicalizar_permisos_configuracion_academica', null)
on conflict(version) do nothing;

commit;

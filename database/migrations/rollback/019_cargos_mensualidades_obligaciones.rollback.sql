-- Rollback Migracion 019: cargos/mensualidades/obligaciones.
-- Elimina cargos, la columna plan_pago_id de matriculas, funciones, permisos
-- y el registro de migracion.

begin;

-- Quitar permisos (los roles_permisos se limpian en cascada por FK).
delete from public.permisos
where codigo in (
  'academico.cargos.ver',
  'academico.cargos.generar',
  'academico.cargos.anular'
);

-- Eliminar funciones RPC.
drop function if exists public.rpc_listar_cargos_matricula(uuid, uuid);
drop function if exists public.rpc_listar_cargos_alumno(uuid, uuid);
drop function if exists public.rpc_resumen_financiero_alumno(uuid, uuid);
drop function if exists public.rpc_asignar_plan_pago_matricula(uuid, uuid, uuid);
drop function if exists public.rpc_generar_cargos_matricula(uuid, uuid);
drop function if exists public.rpc_anular_cargo(uuid, text, uuid);

-- Eliminar triggers y funciones de validacion.
drop trigger if exists trg_cargos_coherencia_before on public.cargos;
drop function if exists public.trg_cargos_coherencia();
drop trigger if exists trg_matriculas_plan_institucion_before on public.matriculas;
drop function if exists public.trg_matriculas_plan_institucion();

-- Eliminar tabla de cargos.
drop table if exists public.cargos;

-- Quitar la columna de plan asignado de matriculas (y sus objetos asociados).
alter table public.matriculas drop column if exists plan_pago_id;

-- Quitar registro de migracion.
delete from public.schema_migrations where version = '019';

commit;

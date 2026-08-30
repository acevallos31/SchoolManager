-- Rollback 003. Ejecutar despues de revertir 004 y solo si no hubo backfill.
do $$
begin
  if exists (select 1 from public.personas)
     or exists (
       select 1 from public.alumnos
       where persona_id is not null
          or institucion_id is not null
          or rne is not null
          or codigo_interno is not null
          or fecha_desactivacion is not null
          or motivo_desactivacion is not null
     )
     or exists (
       select 1 from public.usuarios
       where persona_id is not null
          or auth_user_id is not null
          or fecha_desactivacion is not null
          or motivo_desactivacion is not null
     ) then
    raise exception 'Rollback bloqueado: existen referencias o personas migradas.';
  end if;
end;
$$;
drop index if exists public.ux_personas_documento_normalizado;
drop index if exists public.ux_alumnos_codigo_interno_por_institucion;
drop index if exists public.ux_alumnos_rne_global;
drop index if exists public.ux_alumnos_persona_institucion;
drop index if exists public.ux_usuarios_auth_user_id;
drop index if exists public.ux_usuarios_persona_id;
drop index if exists public.ix_alumnos_persona_id;
drop index if exists public.ix_alumnos_institucion_id;
alter table public.alumnos drop column if exists persona_id;
alter table public.alumnos drop column if exists institucion_id;
alter table public.alumnos drop column if exists rne;
alter table public.alumnos drop column if exists codigo_interno;
alter table public.alumnos drop column if exists fecha_desactivacion;
alter table public.alumnos drop column if exists motivo_desactivacion;
alter table public.usuarios drop column if exists persona_id;
alter table public.usuarios drop column if exists auth_user_id;
alter table public.usuarios drop column if exists fecha_desactivacion;
alter table public.usuarios drop column if exists motivo_desactivacion;
drop table if exists public.personas;

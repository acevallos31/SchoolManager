-- ======================================================================
-- Migracion 028 - RBAC dinamico institucional y Superadministrador.
--
-- Objetivos:
--   * separar roles de plataforma, plantillas, institucionales y legacy;
--   * permitir codigos de rol repetidos entre instituciones distintas;
--   * clasificar permisos por ambito, delegabilidad, riesgo y visibilidad;
--   * crear el rol global protegido platform_admin (Superadministrador);
--   * impedir que un administrador legacy/institucional asigne o retire
--     platform_admin mediante las RPC existentes;
--   * sembrar las plantillas base sin asignarlas directamente a usuarios.
--
-- IMPORTANTE:
--   Esta migracion NO promueve automaticamente ningun usuario existente a
--   platform_admin. El primer Superadministrador debe asignarse de forma
--   explicita y auditada durante el rollout, fuera de la aplicacion.
-- ======================================================================

begin;

do $$
begin
  if not exists (
    select 1 from public.schema_migrations where version = '027'
  ) then
    raise exception 'La migracion 028 requiere la 027 aplicada previamente.';
  end if;
end
$$;

-- ======================================================================
-- 1. Metadatos del catalogo de permisos.
-- ======================================================================
alter table public.permisos
  add column if not exists ambito text not null default 'institucion',
  add column if not exists delegable boolean not null default true,
  add column if not exists riesgo text not null default 'medio',
  add column if not exists estado text not null default 'vigente',
  add column if not exists visible_en_roles boolean not null default true;

do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'ck_permisos_ambito') then
    alter table public.permisos add constraint ck_permisos_ambito
      check (ambito in ('institucion', 'plataforma'));
  end if;
  if not exists (select 1 from pg_constraint where conname = 'ck_permisos_riesgo') then
    alter table public.permisos add constraint ck_permisos_riesgo
      check (riesgo in ('bajo', 'medio', 'alto', 'critico'));
  end if;
  if not exists (select 1 from pg_constraint where conname = 'ck_permisos_estado') then
    alter table public.permisos add constraint ck_permisos_estado
      check (estado in ('vigente', 'deprecado'));
  end if;
  if not exists (select 1 from pg_constraint where conname = 'ck_permisos_plataforma_no_delegable') then
    alter table public.permisos add constraint ck_permisos_plataforma_no_delegable
      check (ambito <> 'plataforma' or delegable = false);
  end if;
end
$$;

-- Operaciones de implementacion/plataforma existentes. Se conserva su grant
-- legacy actual para no romper el rollout; la migracion de usuarios ocurrira
-- en una fase posterior. La metadata ya impide tratarlas como delegables.
update public.permisos
set ambito = 'plataforma', delegable = false,
    riesgo = case when codigo like '%.editar' then 'critico' else 'alto' end
where codigo in (
  'configuracion.sistema.ver',
  'configuracion.sistema.editar',
  'configuracion.instituciones.ver',
  'configuracion.instituciones.editar'
);

-- Namespace historico reemplazado por academico.responsables.* desde 009.
update public.permisos
set estado = 'deprecado', delegable = false, visible_en_roles = false, riesgo = 'bajo'
where codigo like 'responsables.responsables.%';

-- Permisos internos de RPC/DB. Siguen vigentes porque las RPC historicas los
-- validan, pero no deben aparecer como capacidades independientes en la UI.
update public.permisos
set delegable = false, visible_en_roles = false
where codigo like 'configuracion.ciclos.%'
   or codigo like 'configuracion.periodos_matricula.%'
   or codigo like 'configuracion.grados.%'
   or codigo like 'configuracion.jornadas.%'
   or codigo like 'configuracion.secciones.%'
   or codigo like 'academico.secciones.%';

-- Codigos de matricula conservados por compatibilidad; la aplicacion vigente
-- usa academico.matriculas.cambiar_estado para las transiciones.
update public.permisos
set delegable = false, visible_en_roles = false, riesgo = 'alto'
where codigo in ('academico.matriculas.editar', 'academico.matriculas.anular');

-- Gestion de identidad: delegable dentro de una institucion, pero sensible.
update public.permisos
set riesgo = 'alto'
where codigo like 'identidad.usuarios.%'
   or codigo like 'identidad.roles.%';

-- Operaciones financieras mutables: delegables, pero de riesgo alto.
update public.permisos
set riesgo = 'alto'
where codigo in (
  'academico.cargos.generar',
  'academico.cargos.anular',
  'academico.pagos.registrar',
  'academico.pagos.anular',
  'configuracion.conceptos_financieros.crear',
  'configuracion.conceptos_financieros.editar',
  'configuracion.conceptos_financieros.desactivar',
  'configuracion.planes_pago.crear',
  'configuracion.planes_pago.editar',
  'configuracion.planes_pago.desactivar'
);

-- Permisos exclusivos de plataforma. Ningun rol legacy los recibe.
insert into public.permisos
  (codigo, modulo, nombre, descripcion, ambito, delegable, riesgo, estado, visible_en_roles)
values
  ('platform.superadmins.gestionar', 'platform', 'Gestionar Superadministradores',
   'Crear, asignar o retirar el rol global protegido platform_admin.',
   'plataforma', false, 'critico', 'vigente', true),
  ('platform.roles.ver', 'platform', 'Ver roles de plataforma',
   'Consultar roles y plantillas globales de SchoolManager.',
   'plataforma', false, 'alto', 'vigente', true),
  ('platform.roles.editar', 'platform', 'Editar roles de plataforma',
   'Administrar definiciones globales protegidas distintas de platform_admin.',
   'plataforma', false, 'critico', 'vigente', true),
  ('platform.permisos.ver', 'platform', 'Ver catalogo global de permisos',
   'Consultar metadata y clasificacion del catalogo de permisos.',
   'plataforma', false, 'alto', 'vigente', true),
  ('platform.permisos.editar', 'platform', 'Editar catalogo global de permisos',
   'Administrar metadata global de delegacion, riesgo y visibilidad.',
   'plataforma', false, 'critico', 'vigente', true),
  ('platform.auditoria.ver', 'platform', 'Ver auditoria global',
   'Consultar auditoria de seguridad y administracion de plataforma.',
   'plataforma', false, 'alto', 'vigente', true)
on conflict (codigo) do update
set modulo = excluded.modulo,
    nombre = excluded.nombre,
    descripcion = excluded.descripcion,
    ambito = excluded.ambito,
    delegable = excluded.delegable,
    riesgo = excluded.riesgo,
    estado = excluded.estado,
    visible_en_roles = excluded.visible_en_roles;

-- ======================================================================
-- 2. Evolucion de definiciones de rol.
-- ======================================================================
alter table public.roles
  add column if not exists institucion_id uuid null,
  add column if not exists tipo text not null default 'legacy',
  add column if not exists rol_base_id uuid null,
  add column if not exists protegido boolean not null default false,
  add column if not exists plantilla_version integer null;

do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'fk_roles_institucion') then
    alter table public.roles add constraint fk_roles_institucion
      foreign key (institucion_id) references public.instituciones(id) on delete restrict;
  end if;
  if not exists (select 1 from pg_constraint where conname = 'fk_roles_rol_base') then
    alter table public.roles add constraint fk_roles_rol_base
      foreign key (rol_base_id) references public.roles(id) on delete restrict;
  end if;
  if not exists (select 1 from pg_constraint where conname = 'ck_roles_tipo') then
    alter table public.roles add constraint ck_roles_tipo
      check (tipo in ('legacy', 'plataforma', 'plantilla', 'institucional'));
  end if;
  if not exists (select 1 from pg_constraint where conname = 'ck_roles_tipo_ambito') then
    alter table public.roles add constraint ck_roles_tipo_ambito check (
      (tipo in ('legacy', 'plataforma', 'plantilla') and institucion_id is null)
      or (tipo = 'institucional' and institucion_id is not null)
    );
  end if;
  if not exists (select 1 from pg_constraint where conname = 'ck_roles_rol_base_distinto') then
    alter table public.roles add constraint ck_roles_rol_base_distinto
      check (rol_base_id is null or rol_base_id <> id);
  end if;
  if not exists (select 1 from pg_constraint where conname = 'ck_roles_plantilla_version') then
    alter table public.roles add constraint ck_roles_plantilla_version
      check (plantilla_version is null or plantilla_version > 0);
  end if;
end
$$;

-- La unicidad deja de ser global para definiciones institucionales.
alter table public.roles drop constraint if exists uq_roles_codigo;
create unique index if not exists ux_roles_codigo_global
  on public.roles(codigo) where institucion_id is null;
create unique index if not exists ux_roles_institucion_codigo
  on public.roles(institucion_id, codigo) where institucion_id is not null;
create index if not exists ix_roles_institucion_tipo_activo
  on public.roles(institucion_id, tipo, activo);
create index if not exists ix_roles_rol_base_id
  on public.roles(rol_base_id) where rol_base_id is not null;

-- Rol global protegido. No se asigna a ningun usuario en esta migracion.
insert into public.roles
  (codigo, nombre, descripcion, es_sistema, activo, tipo, protegido)
select
  'platform_admin', 'Superadministrador',
  'Administracion global protegida de SchoolManager.',
  true, true, 'plataforma', true
where not exists (
  select 1 from public.roles where codigo = 'platform_admin' and institucion_id is null
);

-- Plantillas globales. No son asignables directamente a usuarios.
insert into public.roles
  (codigo, nombre, descripcion, es_sistema, activo, tipo, protegido, plantilla_version)
select v.codigo, v.nombre, v.descripcion, true, true, 'plantilla', true, 1
from (values
  ('school_admin', 'Administrador institucional', 'Administracion integral de una institucion.'),
  ('school_staff', 'Personal administrativo', 'Operacion escolar cotidiana.'),
  ('academic_coordinator', 'Coordinacion academica', 'Coordinacion de estructura y procesos academicos.'),
  ('finance_operator', 'Operador financiero', 'Cargos, pagos y cobranza institucional.'),
  ('teacher', 'Docente', 'Plantilla para funciones docentes.'),
  ('parent', 'Padre o responsable', 'Portal del responsable con alcance por relacion.'),
  ('student', 'Alumno', 'Portal del alumno con alcance propio.'),
  ('demo_viewer', 'Consulta / demostracion', 'Lectura controlada sin mutaciones.'),
  ('support_agent', 'Soporte institucional', 'Atencion de tickets dentro del alcance permitido.')
) as v(codigo, nombre, descripcion)
where not exists (
  select 1 from public.roles r where r.codigo = v.codigo and r.institucion_id is null
);

-- Superadministrador recibe el catalogo vigente completo, incluido platform.*.
-- Las migraciones futuras deberan conceder permisos nuevos de forma explicita;
-- se elimina el patron historico de otorgarlos automaticamente a 'admin'.
insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'platform_admin'
  and r.tipo = 'plataforma'
  and p.estado = 'vigente'
on conflict do nothing;

-- school_admin: todas las capacidades institucionales vigentes, incluidas las
-- internas de RPC necesarias para mantener compatibilidad durante la transicion.
insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'school_admin'
  and r.tipo = 'plantilla'
  and p.ambito = 'institucion'
  and p.estado = 'vigente'
on conflict do nothing;

-- school_staff conserva la composicion efectiva vigente del operador legacy,
-- excluyendo permisos deprecados.
insert into public.roles_permisos (rol_id, permiso_id)
select destino.id, rp.permiso_id
from public.roles destino
join public.roles origen on origen.codigo = 'operador' and origen.tipo = 'legacy'
join public.roles_permisos rp on rp.rol_id = origen.id
join public.permisos p on p.id = rp.permiso_id and p.estado = 'vigente'
where destino.codigo = 'school_staff' and destino.tipo = 'plantilla'
on conflict do nothing;

-- Coordinacion academica: capacidades academicas y sus permisos internos DB.
insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'academic_coordinator'
  and r.tipo = 'plantilla'
  and p.estado = 'vigente'
  and (
    p.codigo like 'academico.alumnos.%'
    or p.codigo like 'academico.matriculas.%'
    or p.codigo like 'academico.responsables.%'
    or p.codigo like 'academico.secciones.%'
    or p.codigo like 'academico.ciclos.%'
    or p.codigo like 'academico.estructura.%'
    or p.codigo like 'configuracion.ciclos.%'
    or p.codigo like 'configuracion.periodos_matricula.%'
    or p.codigo like 'configuracion.grados.%'
    or p.codigo like 'configuracion.jornadas.%'
    or p.codigo like 'configuracion.secciones.%'
  )
on conflict do nothing;

-- Operacion financiera sin administracion de plataforma.
insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'finance_operator'
  and r.tipo = 'plantilla'
  and p.estado = 'vigente'
  and p.codigo in (
    'academico.alumnos.ver',
    'academico.matriculas.ver',
    'academico.cargos.ver',
    'academico.cargos.generar',
    'academico.cargos.anular',
    'academico.pagos.ver',
    'academico.pagos.registrar',
    'academico.pagos.anular',
    'configuracion.conceptos_financieros.ver',
    'configuracion.planes_pago.ver'
  )
on conflict do nothing;

-- Vista de demostracion: lectura institucional sin catalogos de identidad.
insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'demo_viewer'
  and r.tipo = 'plantilla'
  and p.ambito = 'institucion'
  and p.estado = 'vigente'
  and p.codigo like '%.ver'
  and p.codigo not like 'identidad.%'
on conflict do nothing;

-- ======================================================================
-- 3. Invariante universal de ambito para asignaciones.
-- ======================================================================
create or replace function public.trg_usuarios_roles_validar_ambito_rol()
returns trigger
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_tipo text;
  v_institucion_id uuid;
  v_activo boolean;
begin
  select r.tipo, r.institucion_id, r.activo
    into v_tipo, v_institucion_id, v_activo
  from public.roles r
  where r.id = new.rol_id;

  if not found then
    raise exception 'El rol no existe.' using errcode = '23503';
  end if;

  if new.activo and not v_activo then
    raise exception 'No se puede asignar un rol inactivo.' using errcode = '23514';
  end if;

  if new.activo and v_tipo = 'plantilla' then
    raise exception 'Una plantilla de rol no puede asignarse directamente.' using errcode = '23514';
  end if;

  if v_tipo = 'plataforma' and new.institucion_id is not null then
    raise exception 'Un rol de plataforma solo puede asignarse globalmente.' using errcode = '23514';
  end if;

  if v_tipo = 'institucional'
     and new.institucion_id is distinct from v_institucion_id then
    raise exception 'El rol institucional solo puede asignarse en su propia institucion.'
      using errcode = '23514';
  end if;

  return new;
end
$$;

drop trigger if exists trg_usuarios_roles_validar_ambito_rol_before
  on public.usuarios_roles;
create trigger trg_usuarios_roles_validar_ambito_rol_before
  before insert or update of rol_id, institucion_id, activo
  on public.usuarios_roles
  for each row execute function public.trg_usuarios_roles_validar_ambito_rol();

revoke all on function public.trg_usuarios_roles_validar_ambito_rol()
  from public, anon, authenticated;
grant execute on function public.trg_usuarios_roles_validar_ambito_rol()
  to service_role;

-- ======================================================================
-- 4. Endurecer RPC de asignacion: platform_admin solo por Superadmin.
-- ======================================================================
create or replace function public.rpc_asignar_rol_usuario(
  p_usuario_id uuid, p_rol_codigo text, p_institucion_id uuid default null
)
returns uuid
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_rol_id uuid;
  v_rol_tipo text;
  v_rol_institucion_id uuid;
  v_id uuid;
begin
  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  if not exists (select 1 from public.usuarios where id = p_usuario_id and activo) then
    raise exception 'El usuario destino no existe o esta inactivo.' using errcode = '23503';
  end if;

  if p_institucion_id is not null and not exists (
    select 1 from public.instituciones where id = p_institucion_id and activo
  ) then
    raise exception 'La institucion no existe o esta inactiva.' using errcode = '23503';
  end if;

  -- Prefiere una definicion propia de la institucion. Durante la transicion,
  -- los roles legacy globales siguen siendo un fallback valido.
  select r.id, r.tipo, r.institucion_id
    into v_rol_id, v_rol_tipo, v_rol_institucion_id
  from public.roles r
  where r.codigo = p_rol_codigo
    and r.activo
    and (r.institucion_id is null or r.institucion_id = p_institucion_id)
  order by
    case when r.institucion_id is not distinct from p_institucion_id then 0 else 1 end,
    case r.tipo when 'institucional' then 0 when 'legacy' then 1
      when 'plataforma' then 2 else 3 end
  limit 1;

  if v_rol_id is null then
    raise exception 'El rol no existe o esta inactivo.' using errcode = '23503';
  end if;

  if v_rol_tipo = 'plantilla' then
    raise exception 'Una plantilla debe clonarse antes de asignarse.' using errcode = '23514';
  end if;

  if v_rol_tipo = 'plataforma' then
    if p_institucion_id is not null then
      raise exception 'Un rol de plataforma solo puede asignarse globalmente.' using errcode = '23514';
    end if;

    if p_rol_codigo = 'platform_admin' then
      if not public.usuario_tiene_permiso_actual('platform.superadmins.gestionar', null) then
        raise exception 'Solo un Superadministrador puede asignar platform_admin.'
          using errcode = '42501';
      end if;
    elsif not public.usuario_tiene_permiso_actual('platform.roles.editar', null) then
      raise exception 'Permiso denegado.' using errcode = '42501';
    end if;
  else
    if not public.usuario_tiene_permiso_actual(
      'identidad.usuarios.asignar_roles', p_institucion_id
    ) then
      raise exception 'Permiso denegado.' using errcode = '42501';
    end if;

    if v_rol_tipo = 'institucional'
       and v_rol_institucion_id is distinct from p_institucion_id then
      raise exception 'El rol institucional no pertenece a la institucion indicada.'
        using errcode = '23514';
    end if;
  end if;

  insert into public.usuarios_roles (usuario_id, rol_id, institucion_id)
  values (p_usuario_id, v_rol_id, p_institucion_id)
  returning id into v_id;

  return v_id;
end
$$;

-- ======================================================================
-- 5. Endurecer desactivacion y proteger el ultimo Superadministrador.
-- ======================================================================
create or replace function public.rpc_desactivar_rol_usuario(
  p_usuario_rol_id uuid, p_motivo text
)
returns void
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_institucion_id uuid;
  v_usuario_id uuid;
  v_rol_tipo text;
  v_rol_codigo text;
  v_superadmins_activos integer;
begin
  if p_motivo is null or btrim(p_motivo) = '' then
    raise exception 'El motivo es obligatorio.' using errcode = '22023';
  end if;

  select ur.institucion_id, ur.usuario_id, r.tipo, r.codigo
    into v_institucion_id, v_usuario_id, v_rol_tipo, v_rol_codigo
  from public.usuarios_roles ur
  join public.roles r on r.id = ur.rol_id
  where ur.id = p_usuario_rol_id and ur.activo
  for update of ur;

  if not found then
    raise exception 'La asignacion activa no existe.' using errcode = 'P0002';
  end if;

  if public.usuario_actual_id() is null then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  if v_rol_tipo = 'plataforma' then
    if v_rol_codigo = 'platform_admin' then
      if not public.usuario_tiene_permiso_actual('platform.superadmins.gestionar', null) then
        raise exception 'Solo un Superadministrador puede retirar platform_admin.'
          using errcode = '42501';
      end if;

      -- Solo protege el ultimo Superadministrador cuyo usuario sigue activo.
      if exists (select 1 from public.usuarios where id = v_usuario_id and activo) then
        select count(*)::integer
          into v_superadmins_activos
        from public.usuarios_roles ur
        join public.roles r on r.id = ur.rol_id
        join public.usuarios u on u.id = ur.usuario_id
        where ur.activo
          and ur.institucion_id is null
          and r.activo
          and r.tipo = 'plataforma'
          and r.codigo = 'platform_admin'
          and u.activo;

        if v_superadmins_activos <= 1 then
          raise exception 'No se puede retirar el ultimo Superadministrador activo.'
            using errcode = '23514';
        end if;
      end if;
    elsif not public.usuario_tiene_permiso_actual('platform.roles.editar', null) then
      raise exception 'Permiso denegado.' using errcode = '42501';
    end if;
  elsif not public.usuario_tiene_permiso_actual(
    'identidad.usuarios.asignar_roles', v_institucion_id
  ) then
    raise exception 'Permiso denegado.' using errcode = '42501';
  end if;

  update public.usuarios_roles
  set activo = false,
      fecha_desactivacion = now(),
      motivo_desactivacion = btrim(p_motivo),
      updated_at = now()
  where id = p_usuario_rol_id;
end
$$;

-- Conserva la superficie autenticada previa; la autorizacion ocurre dentro
-- de las RPC y ahora distingue operaciones de plataforma.
revoke execute on function public.rpc_asignar_rol_usuario(uuid, text, uuid)
  from public, anon;
revoke execute on function public.rpc_desactivar_rol_usuario(uuid, text)
  from public, anon;
grant execute on function public.rpc_asignar_rol_usuario(uuid, text, uuid)
  to authenticated, service_role;
grant execute on function public.rpc_desactivar_rol_usuario(uuid, text)
  to authenticated, service_role;

-- ======================================================================
-- 6. Lectura RLS: ocultar roles/permisos de plataforma a no-Superadmin.
-- ======================================================================
drop policy if exists roles_select on public.roles;
create policy roles_select on public.roles for select to authenticated using (
  (
    tipo = 'plataforma'
    and public.usuario_tiene_permiso_actual('platform.roles.ver', null)
  )
  or (
    tipo = 'institucional'
    and public.usuario_tiene_permiso_actual('identidad.roles.ver', institucion_id)
  )
  or (
    tipo in ('legacy', 'plantilla')
    and public.usuario_tiene_permiso_en_algun_ambito('identidad.roles.ver')
  )
);

drop policy if exists permisos_select on public.permisos;
create policy permisos_select on public.permisos for select to authenticated using (
  (
    ambito = 'plataforma'
    and public.usuario_tiene_permiso_actual('platform.permisos.ver', null)
  )
  or (
    ambito = 'institucion'
    and public.usuario_tiene_permiso_en_algun_ambito('identidad.roles.ver')
  )
);

drop policy if exists roles_permisos_select on public.roles_permisos;
create policy roles_permisos_select on public.roles_permisos for select to authenticated using (
  exists (
    select 1
    from public.roles r
    where r.id = roles_permisos.rol_id
      and (
        (r.tipo = 'plataforma'
         and public.usuario_tiene_permiso_actual('platform.roles.ver', null))
        or (r.tipo = 'institucional'
            and public.usuario_tiene_permiso_actual('identidad.roles.ver', r.institucion_id))
        or (r.tipo in ('legacy', 'plantilla')
            and public.usuario_tiene_permiso_en_algun_ambito('identidad.roles.ver'))
      )
  )
);

drop policy if exists usuarios_roles_select on public.usuarios_roles;
create policy usuarios_roles_select on public.usuarios_roles for select to authenticated using (
  usuario_id = public.usuario_actual_id()
  or exists (
    select 1
    from public.roles r
    where r.id = usuarios_roles.rol_id
      and (
        (r.tipo = 'plataforma'
         and public.usuario_tiene_permiso_actual('platform.roles.ver', null))
        or (r.tipo <> 'plataforma'
            and public.usuario_tiene_permiso_actual(
              'identidad.usuarios.ver', usuarios_roles.institucion_id
            ))
      )
  )
);

insert into public.schema_migrations (version, nombre, checksum)
values ('028', 'roles_dinamicos_institucionales', null)
on conflict (version) do nothing;

commit;

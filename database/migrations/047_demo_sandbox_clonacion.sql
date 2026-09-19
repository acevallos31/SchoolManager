-- Migracion 047 - clonacion transaccional de sandbox Demo.
-- 049C: agrega la plantilla demo_operator y operaciones internas de
-- crear/reutilizar/resetear una sandbox a partir de demo_template.
--
-- Seguridad:
-- - Las RPC quedan restringidas a service_role / operaciones backend.
-- - No se habilita Anonymous Sign-In ni se crean plantillas Demo.
-- - Produccion mantiene DemoModeEnabled=false y no invoca estas RPC.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '046') then
    raise exception 'La migracion 047 requiere la 046 aplicada previamente.';
  end if;
end
$$;

-- ---------------------------------------------------------------------
-- Compatibilidad legacy: la bootstrap histórica creó UNIQUE(nombre) para
-- ciclos. El modelo multiinstitución vigente usa UNIQUE(institucion_id,nombre).
-- Producción ya está normalizada; IF EXISTS hace este ajuste inocuo allí.
-- ---------------------------------------------------------------------
alter table public.ciclos_escolares
  drop constraint if exists ciclos_escolares_nombre_key;

do $$
begin
  if not exists (
    select 1
    from pg_constraint
    where conname='uq_ciclos_escolares_institucion_nombre'
      and conrelid='public.ciclos_escolares'::regclass
  ) then
    alter table public.ciclos_escolares
      add constraint uq_ciclos_escolares_institucion_nombre
      unique (institucion_id,nombre);
  end if;
end
$$;

-- ---------------------------------------------------------------------
-- 1. Plantilla RBAC para el visitante Demo.
-- ---------------------------------------------------------------------
insert into public.roles(
  codigo, nombre, descripcion, es_sistema, activo, institucion_id,
  tipo, protegido, plantilla_version
)
select
  'demo_operator',
  'Operador Demo',
  'Operacion funcional dentro de una sandbox Demo, sin administracion de identidad ni plataforma.',
  true, true, null, 'plantilla', true, 1
where not exists (
  select 1
  from public.roles
  where codigo = 'demo_operator'
    and institucion_id is null
);

insert into public.roles_permisos(rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'demo_operator'
  and r.tipo = 'plantilla'
  and r.institucion_id is null
  and p.ambito = 'institucion'
  and p.estado = 'vigente'
  and p.codigo not like 'identidad.%'
  and p.codigo <> 'configuracion.sistema.editar'
on conflict do nothing;

-- ---------------------------------------------------------------------
-- 2. Crear o reutilizar sandbox para un Auth UID anonimo.
-- ---------------------------------------------------------------------
create or replace function public.rpc_crear_sandbox_demo(
  p_auth_user_id uuid,
  p_plantilla_institucion_id uuid
)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_ahora timestamptz := pg_catalog.clock_timestamp();
  v_sesion_id uuid;
  v_sandbox_id uuid;
  v_sandbox_anterior uuid;
  v_usuario_id uuid;
  v_persona_usuario_id uuid;
  v_rol_base_id uuid;
  v_rol_base_version integer;
  v_rol_demo_id uuid := pg_catalog.gen_random_uuid();
  v_sufijo text;
begin
  if p_auth_user_id is null or p_plantilla_institucion_id is null then
    raise exception 'Auth user y plantilla Demo son obligatorios.'
      using errcode = '22023';
  end if;

  perform pg_catalog.pg_advisory_xact_lock(
    pg_catalog.hashtextextended(p_auth_user_id::text, 49049)
  );

  if not exists (
    select 1
    from public.instituciones i
    where i.id = p_plantilla_institucion_id
      and i.tipo = 'demo_template'
      and i.activo
  ) then
    raise exception 'La plantilla Demo no existe o no esta activa.'
      using errcode = 'P0002';
  end if;

  -- Reutiliza una sesion aun vigente y renueva solo el timeout por inactividad.
  select ds.id, ds.institucion_id
    into v_sesion_id, v_sandbox_id
  from public.demo_sessions ds
  where ds.auth_user_id = p_auth_user_id
    and ds.estado = 'activa'
  for update;

  if found then
    if exists (
      select 1
      from public.demo_sessions ds
      where ds.id = v_sesion_id
        and ds.expires_at > v_ahora
        and ds.max_expires_at > v_ahora
    ) then
      update public.demo_sessions
      set last_activity_at = v_ahora,
          expires_at = least(v_ahora + interval '2 hours', max_expires_at)
      where id = v_sesion_id;

      return pg_catalog.jsonb_build_object(
        'sessionId', v_sesion_id,
        'institucionId', v_sandbox_id,
        'reused', true
      );
    end if;

    v_sandbox_anterior := v_sandbox_id;

    update public.demo_sessions
    set estado = 'expirada',
        closed_at = v_ahora,
        last_activity_at = v_ahora
    where id = v_sesion_id;
  end if;

  select u.id, u.persona_id
    into v_usuario_id, v_persona_usuario_id
  from public.usuarios u
  where u.auth_user_id = p_auth_user_id
  for update;

  if found then
    if not exists (
      select 1 from public.usuarios u
      where u.id = v_usuario_id and u.activo
    ) then
      raise exception 'El usuario interno Demo esta inactivo.'
        using errcode = '42501';
    end if;

    if exists (
      select 1
      from public.usuarios_roles ur
      left join public.instituciones i on i.id = ur.institucion_id
      where ur.usuario_id = v_usuario_id
        and ur.activo
        and (
          ur.institucion_id is null
          or i.tipo is distinct from 'demo_sandbox'
        )
    ) then
      raise exception 'La identidad ya pertenece a un usuario no exclusivo de Demo.'
        using errcode = '23514';
    end if;
  else
    v_persona_usuario_id := pg_catalog.gen_random_uuid();
    v_usuario_id := pg_catalog.gen_random_uuid();

    insert into public.personas(
      id, nombres, apellidos, estado
    ) values (
      v_persona_usuario_id, 'Visitante', 'Demo', 'activo'
    );

    insert into public.usuarios(
      id, persona_id, auth_user_id, activo
    ) values (
      v_usuario_id, v_persona_usuario_id, p_auth_user_id, true
    );
  end if;

  -- Una sesion expirada deja de otorgar contexto institucional.
  if v_sandbox_anterior is not null then
    update public.usuarios_roles
    set activo = false,
        fecha_desactivacion = v_ahora,
        motivo_desactivacion = 'Sesion Demo expirada',
        updated_at = v_ahora
    where usuario_id = v_usuario_id
      and institucion_id = v_sandbox_anterior
      and activo;

    update public.instituciones
    set activo = false,
        fecha_desactivacion = v_ahora,
        motivo_desactivacion = 'Sandbox Demo expirada',
        updated_at = v_ahora
    where id = v_sandbox_anterior
      and tipo = 'demo_sandbox'
      and activo;
  end if;

  select r.id, r.plantilla_version
    into v_rol_base_id, v_rol_base_version
  from public.roles r
  where r.codigo = 'demo_operator'
    and r.tipo = 'plantilla'
    and r.institucion_id is null
    and r.activo;

  if v_rol_base_id is null then
    raise exception 'Falta la plantilla RBAC demo_operator.'
      using errcode = 'SM001';
  end if;

  v_sandbox_id := pg_catalog.gen_random_uuid();
  v_sufijo := pg_catalog.upper(pg_catalog.substr(
    pg_catalog.replace(v_sandbox_id::text, '-', ''), 1, 6
  ));

  insert into public.instituciones(
    id, nombre, nombre_corto, direccion, telefono, correo, logo_url, activo, tipo
  )
  select
    v_sandbox_id,
    i.nombre || ' · Demo ' || v_sufijo,
    case
      when i.nombre_corto is null then 'Demo ' || v_sufijo
      else i.nombre_corto || ' Demo'
    end,
    i.direccion,
    i.telefono,
    i.correo,
    i.logo_url,
    true,
    'demo_sandbox'
  from public.instituciones i
  where i.id = p_plantilla_institucion_id;

  insert into public.configuracion_identificadores(
    institucion_id, rne_requerido, identificacion_civil_requerida,
    codigo_interno_requerido, tipos_identificacion_permitidos
  )
  select
    v_sandbox_id, c.rne_requerido, c.identificacion_civil_requerida,
    c.codigo_interno_requerido, c.tipos_identificacion_permitidos
  from public.configuracion_identificadores c
  where c.institucion_id = p_plantilla_institucion_id;

  -- Rol institucional acotado: no identidad.*, no plataforma.
  insert into public.roles(
    id, codigo, nombre, descripcion, es_sistema, activo, institucion_id,
    tipo, protegido, rol_base_id, plantilla_version
  ) values (
    v_rol_demo_id, 'demo_operator', 'Operador Demo',
    'Rol temporal de operacion funcional dentro de una sandbox Demo.',
    false, true, v_sandbox_id, 'institucional', true,
    v_rol_base_id, v_rol_base_version
  );

  insert into public.roles_permisos(rol_id, permiso_id)
  select v_rol_demo_id, rp.permiso_id
  from public.roles_permisos rp
  where rp.rol_id = v_rol_base_id
  on conflict do nothing;

  insert into public.usuarios_roles(
    usuario_id, rol_id, institucion_id, activo
  ) values (
    v_usuario_id, v_rol_demo_id, v_sandbox_id, true
  );

  -- Mapeos UUID origen -> destino. Son temporales y desaparecen al commit.
  create temporary table demo_map_ciclos(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_periodos(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_grados(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_jornadas(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_secciones(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_conceptos(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_planes(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_personas(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_alumnos(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_responsables(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_matriculas(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_cargos(
    origen uuid primary key, destino uuid not null
  ) on commit drop;
  create temporary table demo_map_pagos(
    origen uuid primary key, destino uuid not null
  ) on commit drop;

  insert into pg_temp.demo_map_ciclos
  select c.id, pg_catalog.gen_random_uuid()
  from public.ciclos_escolares c
  where c.institucion_id = p_plantilla_institucion_id;

  insert into public.ciclos_escolares(
    id, institucion_id, nombre, fecha_inicio, fecha_fin, activo,
    fecha_desactivacion, motivo_desactivacion
  )
  select
    m.destino, v_sandbox_id, c.nombre, c.fecha_inicio, c.fecha_fin, c.activo,
    c.fecha_desactivacion, c.motivo_desactivacion
  from public.ciclos_escolares c
  join pg_temp.demo_map_ciclos m on m.origen = c.id;

  insert into pg_temp.demo_map_periodos
  select p.id, pg_catalog.gen_random_uuid()
  from public.periodos_matricula p
  join public.ciclos_escolares c on c.id = p.ciclo_id
  where c.institucion_id = p_plantilla_institucion_id;

  insert into public.periodos_matricula(
    id, ciclo_id, nombre, tipo, fecha_inicio, fecha_fin, activo
  )
  select
    mp.destino, mc.destino, p.nombre, p.tipo, p.fecha_inicio, p.fecha_fin, p.activo
  from public.periodos_matricula p
  join pg_temp.demo_map_periodos mp on mp.origen = p.id
  join pg_temp.demo_map_ciclos mc on mc.origen = p.ciclo_id;

  insert into pg_temp.demo_map_grados
  select g.id, pg_catalog.gen_random_uuid()
  from public.grados g
  where g.institucion_id = p_plantilla_institucion_id;

  insert into public.grados(
    id, nombre, orden, activo, institucion_id
  )
  select
    m.destino, g.nombre, g.orden, g.activo, v_sandbox_id
  from public.grados g
  join pg_temp.demo_map_grados m on m.origen = g.id;

  insert into pg_temp.demo_map_jornadas
  select j.id, pg_catalog.gen_random_uuid()
  from public.jornadas j
  where j.institucion_id = p_plantilla_institucion_id;

  insert into public.jornadas(
    id, nombre, activo, institucion_id
  )
  select
    m.destino, j.nombre, j.activo, v_sandbox_id
  from public.jornadas j
  join pg_temp.demo_map_jornadas m on m.origen = j.id;

  insert into pg_temp.demo_map_secciones
  select s.id, pg_catalog.gen_random_uuid()
  from public.secciones s
  where s.institucion_id = p_plantilla_institucion_id;

  insert into public.secciones(
    id, grado_id, jornada_id, nombre, cupo, activo,
    institucion_id, ciclo_id, fecha_desactivacion, motivo_desactivacion
  )
  select
    ms.destino,
    mg.destino,
    mj.destino,
    s.nombre,
    s.cupo,
    s.activo,
    v_sandbox_id,
    mc.destino,
    s.fecha_desactivacion,
    s.motivo_desactivacion
  from public.secciones s
  join pg_temp.demo_map_secciones ms on ms.origen = s.id
  join pg_temp.demo_map_grados mg on mg.origen = s.grado_id
  join pg_temp.demo_map_ciclos mc on mc.origen = s.ciclo_id
  left join pg_temp.demo_map_jornadas mj on mj.origen = s.jornada_id;

  insert into pg_temp.demo_map_conceptos
  select c.id, pg_catalog.gen_random_uuid()
  from public.conceptos_financieros c
  where c.institucion_id = p_plantilla_institucion_id;

  insert into public.conceptos_financieros(
    id, institucion_id, nombre, descripcion, monto, activo,
    fecha_desactivacion, motivo_desactivacion
  )
  select
    m.destino, v_sandbox_id, c.nombre, c.descripcion, c.monto, c.activo,
    c.fecha_desactivacion, c.motivo_desactivacion
  from public.conceptos_financieros c
  join pg_temp.demo_map_conceptos m on m.origen = c.id;

  insert into pg_temp.demo_map_planes
  select p.id, pg_catalog.gen_random_uuid()
  from public.planes_pago p
  where p.institucion_id = p_plantilla_institucion_id;

  insert into public.planes_pago(
    id, institucion_id, nombre, descripcion, activo,
    fecha_desactivacion, motivo_desactivacion
  )
  select
    m.destino, v_sandbox_id, p.nombre, p.descripcion, p.activo,
    p.fecha_desactivacion, p.motivo_desactivacion
  from public.planes_pago p
  join pg_temp.demo_map_planes m on m.origen = p.id;

  insert into public.plan_cuotas(
    id, plan_id, orden, concepto_id, descripcion, monto, vencimiento_dias
  )
  select
    pg_catalog.gen_random_uuid(),
    mp.destino,
    pc.orden,
    mc.destino,
    pc.descripcion,
    pc.monto,
    pc.vencimiento_dias
  from public.plan_cuotas pc
  join pg_temp.demo_map_planes mp on mp.origen = pc.plan_id
  left join pg_temp.demo_map_conceptos mc on mc.origen = pc.concepto_id;

  insert into pg_temp.demo_map_personas
  select p.id, pg_catalog.gen_random_uuid()
  from public.personas p
  where exists (
      select 1 from public.alumnos a
      where a.persona_id = p.id
        and a.institucion_id = p_plantilla_institucion_id
    )
    or exists (
      select 1 from public.responsables r
      where r.persona_id = p.id
        and r.institucion_id = p_plantilla_institucion_id
    );

  insert into public.personas(
    id, nombres, apellidos, tipo_identificacion,
    numero_identificacion, numero_identificacion_normalizado,
    pais_emisor, telefono, correo, direccion, estado,
    fecha_desactivacion, motivo_desactivacion
  )
  select
    m.destino,
    p.nombres,
    p.apellidos,
    p.tipo_identificacion,
    case
      when p.numero_identificacion_normalizado is null then null
      else 'DEMO-' || pg_catalog.upper(pg_catalog.substr(
        pg_catalog.replace(m.destino::text, '-', ''), 1, 12
      ))
    end,
    case
      when p.numero_identificacion_normalizado is null then null
      else 'DEMO-' || pg_catalog.upper(pg_catalog.substr(
        pg_catalog.replace(m.destino::text, '-', ''), 1, 12
      ))
    end,
    p.pais_emisor,
    p.telefono,
    case
      when p.correo is null then null
      else 'demo.' || pg_catalog.substr(
        pg_catalog.replace(m.destino::text, '-', ''), 1, 12
      ) || '@example.invalid'
    end,
    p.direccion,
    p.estado,
    p.fecha_desactivacion,
    p.motivo_desactivacion
  from public.personas p
  join pg_temp.demo_map_personas m on m.origen = p.id;

  insert into pg_temp.demo_map_alumnos
  select a.id, pg_catalog.gen_random_uuid()
  from public.alumnos a
  where a.institucion_id = p_plantilla_institucion_id;

  insert into public.alumnos(
    id, persona_id, institucion_id, rne, codigo_interno,
    fecha_nacimiento, estado, fecha_desactivacion, motivo_desactivacion
  )
  select
    ma.destino,
    mp.destino,
    v_sandbox_id,
    case
      when a.rne is null then null
      else 'DEMO-' || pg_catalog.upper(pg_catalog.substr(
        pg_catalog.replace(ma.destino::text, '-', ''), 1, 12
      ))
    end,
    a.codigo_interno,
    a.fecha_nacimiento,
    a.estado,
    a.fecha_desactivacion,
    a.motivo_desactivacion
  from public.alumnos a
  join pg_temp.demo_map_alumnos ma on ma.origen = a.id
  join pg_temp.demo_map_personas mp on mp.origen = a.persona_id;

  insert into pg_temp.demo_map_responsables
  select r.id, pg_catalog.gen_random_uuid()
  from public.responsables r
  where r.institucion_id = p_plantilla_institucion_id;

  insert into public.responsables(
    id, persona_id, institucion_id, estado,
    fecha_desactivacion, motivo_desactivacion
  )
  select
    mr.destino, mp.destino, v_sandbox_id, r.estado,
    r.fecha_desactivacion, r.motivo_desactivacion
  from public.responsables r
  join pg_temp.demo_map_responsables mr on mr.origen = r.id
  join pg_temp.demo_map_personas mp on mp.origen = r.persona_id;

  insert into public.alumno_responsable(
    id, alumno_id, responsable_id, parentesco, es_principal,
    acceso_financiero, estado, fecha_desactivacion, motivo_desactivacion
  )
  select
    pg_catalog.gen_random_uuid(),
    ma.destino,
    mr.destino,
    ar.parentesco,
    ar.es_principal,
    ar.acceso_financiero,
    ar.estado,
    ar.fecha_desactivacion,
    ar.motivo_desactivacion
  from public.alumno_responsable ar
  join pg_temp.demo_map_alumnos ma on ma.origen = ar.alumno_id
  join pg_temp.demo_map_responsables mr on mr.origen = ar.responsable_id;

  insert into pg_temp.demo_map_matriculas
  select m.id, pg_catalog.gen_random_uuid()
  from public.matriculas m
  where m.institucion_id = p_plantilla_institucion_id;

  insert into public.matriculas(
    id, alumno_id, ciclo_id, seccion_id, periodo_matricula_id,
    registrado_por, fecha_matricula, estado, fecha_anulacion,
    motivo_anulacion, institucion_id, plan_pago_id
  )
  select
    mm.destino,
    ma.destino,
    mc.destino,
    ms.destino,
    mp.destino,
    v_usuario_id,
    m.fecha_matricula,
    m.estado,
    m.fecha_anulacion,
    m.motivo_anulacion,
    v_sandbox_id,
    mplan.destino
  from public.matriculas m
  join pg_temp.demo_map_matriculas mm on mm.origen = m.id
  join pg_temp.demo_map_alumnos ma on ma.origen = m.alumno_id
  join pg_temp.demo_map_ciclos mc on mc.origen = m.ciclo_id
  join pg_temp.demo_map_secciones ms on ms.origen = m.seccion_id
  join pg_temp.demo_map_periodos mp on mp.origen = m.periodo_matricula_id
  left join pg_temp.demo_map_planes mplan on mplan.origen = m.plan_pago_id;

  -- Se recrea un historial minimo coherente; no se copian actores del template.
  insert into public.matricula_estado_historial(
    matricula_id, estado_anterior, estado_nuevo, fecha, usuario_id, motivo
  )
  select
    mm.destino, null, m.estado, m.created_at, v_usuario_id,
    'Estado inicial de sandbox Demo'
  from public.matriculas m
  join pg_temp.demo_map_matriculas mm on mm.origen = m.id;

  insert into pg_temp.demo_map_cargos
  select c.id, pg_catalog.gen_random_uuid()
  from public.cargos c
  where c.institucion_id = p_plantilla_institucion_id;

  insert into public.cargos(
    id, institucion_id, matricula_id, alumno_id, plan_pago_id,
    concepto_id, orden, concepto_nombre, descripcion, monto_original,
    fecha_vencimiento, estado, fecha_generacion, fecha_anulacion,
    motivo_anulacion
  )
  select
    mcargo.destino,
    v_sandbox_id,
    mm.destino,
    ma.destino,
    mplan.destino,
    mconcepto.destino,
    c.orden,
    c.concepto_nombre,
    c.descripcion,
    c.monto_original,
    c.fecha_vencimiento,
    c.estado,
    c.fecha_generacion,
    c.fecha_anulacion,
    c.motivo_anulacion
  from public.cargos c
  join pg_temp.demo_map_cargos mcargo on mcargo.origen = c.id
  join pg_temp.demo_map_matriculas mm on mm.origen = c.matricula_id
  join pg_temp.demo_map_alumnos ma on ma.origen = c.alumno_id
  join pg_temp.demo_map_planes mplan on mplan.origen = c.plan_pago_id
  left join pg_temp.demo_map_conceptos mconcepto on mconcepto.origen = c.concepto_id;

  insert into pg_temp.demo_map_pagos
  select p.id, pg_catalog.gen_random_uuid()
  from public.pagos p
  where p.institucion_id = p_plantilla_institucion_id;

  insert into public.pagos(
    id, institucion_id, alumno_id, responsable_id,
    monto_total, fecha_pago, metodo_pago, referencia_externa,
    estado, registrado_por, fecha_anulacion, anulado_por, motivo_anulacion
  )
  select
    mpago.destino,
    v_sandbox_id,
    ma.destino,
    mr.destino,
    p.monto_total,
    p.fecha_pago,
    p.metodo_pago,
    null,
    p.estado,
    v_usuario_id,
    p.fecha_anulacion,
    case when p.anulado_por is null then null else v_usuario_id end,
    p.motivo_anulacion
  from public.pagos p
  join pg_temp.demo_map_pagos mpago on mpago.origen = p.id
  join pg_temp.demo_map_alumnos ma on ma.origen = p.alumno_id
  left join pg_temp.demo_map_responsables mr on mr.origen = p.responsable_id;

  insert into public.pagos_aplicaciones(
    id, pago_id, cargo_id, institucion_id, monto_aplicado,
    estado, fecha_reversion
  )
  select
    pg_catalog.gen_random_uuid(),
    mp.destino,
    mc.destino,
    v_sandbox_id,
    pa.monto_aplicado,
    pa.estado,
    pa.fecha_reversion
  from public.pagos_aplicaciones pa
  join pg_temp.demo_map_pagos mp on mp.origen = pa.pago_id
  join pg_temp.demo_map_cargos mc on mc.origen = pa.cargo_id
  where pa.institucion_id = p_plantilla_institucion_id;

  insert into public.demo_sessions(
    auth_user_id, institucion_id, plantilla_institucion_id,
    estado, created_at, last_activity_at, expires_at, max_expires_at
  ) values (
    p_auth_user_id, v_sandbox_id, p_plantilla_institucion_id,
    'activa', v_ahora, v_ahora,
    v_ahora + interval '2 hours',
    v_ahora + interval '24 hours'
  )
  returning id into v_sesion_id;

  return pg_catalog.jsonb_build_object(
    'sessionId', v_sesion_id,
    'institucionId', v_sandbox_id,
    'reused', false
  );
end
$$;

-- ---------------------------------------------------------------------
-- 3. Reset: cierra la sandbox actual y crea/reutiliza una nueva.
-- ---------------------------------------------------------------------
create or replace function public.rpc_reset_sandbox_demo(
  p_auth_user_id uuid,
  p_plantilla_institucion_id uuid
)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_ahora timestamptz := pg_catalog.clock_timestamp();
  v_sesion_id uuid;
  v_sandbox_id uuid;
  v_usuario_id uuid;
begin
  if p_auth_user_id is null or p_plantilla_institucion_id is null then
    raise exception 'Auth user y plantilla Demo son obligatorios.'
      using errcode = '22023';
  end if;

  select ds.id, ds.institucion_id
    into v_sesion_id, v_sandbox_id
  from public.demo_sessions ds
  where ds.auth_user_id = p_auth_user_id
    and ds.estado = 'activa'
  for update;

  if found then
    update public.demo_sessions
    set estado = 'reiniciada',
        closed_at = v_ahora,
        last_activity_at = v_ahora
    where id = v_sesion_id;

    select u.id into v_usuario_id
    from public.usuarios u
    where u.auth_user_id = p_auth_user_id
      and u.activo;

    if v_usuario_id is not null then
      update public.usuarios_roles
      set activo = false,
          fecha_desactivacion = v_ahora,
          motivo_desactivacion = 'Sandbox Demo reiniciada',
          updated_at = v_ahora
      where usuario_id = v_usuario_id
        and institucion_id = v_sandbox_id
        and activo;
    end if;

    update public.instituciones
    set activo = false,
        fecha_desactivacion = v_ahora,
        motivo_desactivacion = 'Sandbox Demo reiniciada',
        updated_at = v_ahora
    where id = v_sandbox_id
      and tipo = 'demo_sandbox'
      and activo;
  end if;

  return public.rpc_crear_sandbox_demo(
    p_auth_user_id,
    p_plantilla_institucion_id
  );
end
$$;

revoke all on function public.rpc_crear_sandbox_demo(uuid, uuid)
  from public, anon, authenticated;
revoke all on function public.rpc_reset_sandbox_demo(uuid, uuid)
  from public, anon, authenticated;

grant execute on function public.rpc_crear_sandbox_demo(uuid, uuid)
  to service_role;
grant execute on function public.rpc_reset_sandbox_demo(uuid, uuid)
  to service_role;

insert into public.schema_migrations(version, nombre, checksum)
values ('047', 'demo_sandbox_clonacion', null)
on conflict (version) do nothing;

commit;

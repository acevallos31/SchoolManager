-- Queries críticas del benchmark (PERF-01). Iguales a las que ejecutan el
-- controller / frontend. Cada una es una sentencia independiente para poder
-- medir y EXPLAINar por separado. Se ejecutan via \timing en psql.
\timing on

-- Q1: listar alumnos (carga completa: el problema de fondo en el frontend Supabase)
select count(*) from (
  select a.id, a.persona_id, a.rne, a.estado,
         p.nombres, p.apellidos, p.numero_identificacion
  from public.alumnos a
  join public.personas p on p.id = a.persona_id
) t;

-- Q2: listar matrículas (lectura base del controller con joins)
select count(*) from (
  select m.id, m.alumno_id, m.institucion_id, m.ciclo_id, m.seccion_id,
         m.periodo_matricula_id, m.fecha_matricula, m.estado,
         (trim(coalesce(p.nombres,'')) || ' ' || trim(coalesce(p.apellidos,''))) as nombre_alumno,
         s.nombre as nombre_seccion, g.nombre as nombre_grado,
         j.nombre as nombre_jornada, pm.nombre as nombre_periodo, c.nombre as ciclo_nombre
  from public.matriculas m
  join public.alumnos a on a.id = m.alumno_id
  join public.personas p on p.id = a.persona_id
  join public.secciones s on s.id = m.seccion_id
  left join public.grados g on g.id = s.grado_id
  left join public.jornadas j on j.id = s.jornada_id
  join public.periodos_matricula pm on pm.id = m.periodo_matricula_id
  join public.ciclos_escolares c on c.id = m.ciclo_id
) t;

-- Q3: filtro por estado + ciclo + institución (patrón real del GET con filtros)
select count(*) from public.matriculas m
  where m.institucion_id = (select id from public.instituciones limit 1)
    and m.ciclo_id in (select id from public.ciclos_escolares limit 3)
    and m.estado = 'activa';

-- Q4: filtro por alumno (detalle de matrículas de un alumno)
select count(*) from public.matriculas m
  join public.secciones s on s.id = m.seccion_id
  where m.alumno_id = (select id from public.alumnos limit 1);

-- Q5: catálogo de grados (baja mutación)
select count(*) from public.grados g;
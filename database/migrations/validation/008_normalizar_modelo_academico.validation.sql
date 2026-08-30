-- Validacion 008: todas las consultas de diagnostico deben devolver cero filas.
select id from public.secciones where institucion_id is null or ciclo_id is null;

select s.id
from public.secciones s
join public.ciclos_escolares c on c.id = s.ciclo_id
where s.institucion_id <> c.institucion_id;

select institucion_id, ciclo_id, grado_id, jornada_id, lower(nombre), count(*)
from public.secciones
group by institucion_id, ciclo_id, grado_id, jornada_id, lower(nombre)
having count(*) > 1;

select m.id
from public.matriculas m
join public.secciones s on s.id = m.seccion_id
join public.periodos_matricula pm on pm.id = m.periodo_matricula_id
join public.alumnos a on a.id = m.alumno_id
where m.ciclo_id <> s.ciclo_id
   or m.institucion_id <> s.institucion_id
   or m.institucion_id <> a.institucion_id
   or m.ciclo_id <> pm.ciclo_id;

select alumno_id, ciclo_id, count(*)
from public.matriculas group by alumno_id, ciclo_id having count(*) > 1;

select id, estado from public.matriculas
where estado not in ('pendiente', 'activa', 'finalizada', 'retirada', 'anulada', 'trasladada');

select p.proname
from (values
  ('crear_seccion'), ('matricular_alumno'), ('cambiar_estado_matricula'),
  ('desactivar_alumno'), ('reactivar_alumno'), ('desactivar_seccion'),
  ('crear_alumno_nueva_persona'), ('crear_alumno_para_persona')
) esperado(nombre)
left join pg_proc p on p.proname = esperado.nombre and p.pronamespace = 'public'::regnamespace
where p.oid is null;

select 'trigger_historial_faltante' as problema
where not exists (
  select 1 from pg_trigger
  where tgrelid = 'public.matriculas'::regclass
    and tgname = 'trg_matriculas_historial_estado'
    and not tgisinternal
);

select esperado.nombre as constraint_o_indice_faltante
from (values
  ('uq_matriculas_alumno_ciclo'),
  ('fk_matriculas_alumno_institucion'),
  ('fk_matriculas_seccion_contexto'),
  ('fk_matriculas_periodo_ciclo'),
  ('ck_matriculas_estado'),
  ('ck_secciones_cupo'),
  ('ux_secciones_contexto_jornada_nombre'),
  ('ux_secciones_contexto_sin_jornada_nombre')
) esperado(nombre)
where not exists (
  select 1 from pg_constraint where conname = esperado.nombre
  union all
  select 1 from pg_indexes where schemaname = 'public' and indexname = esperado.nombre
);

select conname, confdeltype
from pg_constraint
where conname in (
  'fk_matriculas_alumno_institucion',
  'fk_matriculas_seccion_contexto',
  'fk_matriculas_periodo_ciclo',
  'fk_matricula_historial_matricula'
)
and confdeltype <> 'r';

select version, nombre from public.schema_migrations where version = '008';

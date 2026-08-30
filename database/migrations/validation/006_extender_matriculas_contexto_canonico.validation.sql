-- Validacion 006. Diagnostico de datos; no modifica matriculas.
select id, alumno_id, ciclo_id
from public.matriculas
where periodo_matricula_id is null;

select m.id, m.ciclo_id, pm.ciclo_id as ciclo_periodo
from public.matriculas m
join public.periodos_matricula pm on pm.id = m.periodo_matricula_id
where m.periodo_matricula_id is not null
  and m.ciclo_id <> pm.ciclo_id;

select m.id, m.grado_id, m.seccion_id, s.grado_id as grado_seccion
from public.matriculas m
join public.secciones s on s.id = m.seccion_id
where m.grado_id is not null
  and s.grado_id is not null
  and m.grado_id <> s.grado_id;

select alumno_id, ciclo_id, count(*) as cantidad
from public.matriculas
group by alumno_id, ciclo_id
having count(*) > 1;
-- Validacion 004: detectar duplicidad de responsables principales activos.
select alumno_id, count(*) as principales_activos
from public.alumno_responsable where es_principal = true and estado = 'activo'
group by alumno_id having count(*) > 1;
select id, padres_encargados from public.alumnos
where padres_encargados is not null and btrim(padres_encargados) <> '';

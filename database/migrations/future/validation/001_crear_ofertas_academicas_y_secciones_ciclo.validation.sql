-- Validacion de migracion FUTURA / POSPUESTA.
select oferta_academica_id, seccion_id, jornada_id, count(*) as cantidad
from public.secciones_ciclo group by oferta_academica_id, seccion_id, jornada_id having count(*) > 1;
select sc.id, oa.grado_id as grado_oferta, s.grado_id as grado_seccion
from public.secciones_ciclo sc
join public.ofertas_academicas oa on oa.id = sc.oferta_academica_id
join public.secciones s on s.id = sc.seccion_id
where s.grado_id is not null and s.grado_id <> oa.grado_id;
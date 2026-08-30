-- Validacion 003: revisar referencias, documento coherente y unicidades de transicion.
select count(*) as alumnos_sin_persona from public.alumnos where persona_id is null;
select count(*) as usuarios_sin_persona from public.usuarios where persona_id is null;
select tipo_identificacion, pais_emisor, numero_identificacion_normalizado, count(*) as cantidad
from public.personas where numero_identificacion_normalizado is not null
group by tipo_identificacion, pais_emisor, numero_identificacion_normalizado having count(*) > 1;
select rne, count(*) as cantidad
from public.alumnos where rne is not null
group by rne having count(*) > 1;
select id from public.personas
where (numero_identificacion is null and (tipo_identificacion is not null or numero_identificacion_normalizado is not null))
	or (numero_identificacion is not null and (tipo_identificacion is null or numero_identificacion_normalizado is null));
select persona_id, institucion_id, count(*) as cantidad
from public.alumnos where persona_id is not null and institucion_id is not null
group by persona_id, institucion_id having count(*) > 1;
select persona_id, count(*) as cantidad
from public.usuarios where persona_id is not null
group by persona_id having count(*) > 1;

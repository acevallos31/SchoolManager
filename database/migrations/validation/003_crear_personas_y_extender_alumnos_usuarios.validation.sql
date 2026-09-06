-- Validacion 003: referencias, documento coherente y unicidades de transicion.
-- Contrato: toda consulta debe devolver cero filas cuando el esquema es correcto.

select '003' as alumnos_sin_persona
where exists (select 1 from public.alumnos where persona_id is null);

select '003' as usuarios_sin_persona
where exists (select 1 from public.usuarios where persona_id is null);

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

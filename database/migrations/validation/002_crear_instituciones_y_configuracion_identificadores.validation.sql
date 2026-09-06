-- Validacion 002: instituciones y configuracion de identificadores coherentes.
-- Contrato: toda consulta debe devolver cero filas cuando el esquema es correcto.

select '002' as tablas_faltantes
where to_regclass('public.instituciones') is null
   or to_regclass('public.configuracion_identificadores') is null;

select institucion_id
from public.configuracion_identificadores
where rne_requerido is null
   or identificacion_civil_requerida is null
   or codigo_interno_requerido is null;

select institucion_id
from public.configuracion_identificadores
group by institucion_id
having count(*) > 1;

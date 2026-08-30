-- Validacion 002: revisar instituciones y configuraciones sin crear datos.
select id, nombre, activo from public.instituciones order by nombre;
select institucion_id, rne_requerido, identificacion_civil_requerida, codigo_interno_requerido
from public.configuracion_identificadores;

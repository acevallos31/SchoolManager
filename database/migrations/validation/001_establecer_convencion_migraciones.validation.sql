-- Validacion 001: la tabla debe existir y no debe haber versiones duplicadas.
-- Contrato: toda consulta debe devolver cero filas cuando el esquema es correcto.

select '001' as tabla_migraciones_faltante
where to_regclass('public.schema_migrations') is null;

select version
from public.schema_migrations
group by version
having count(*) > 1;

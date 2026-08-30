-- Validacion 001: la tabla debe existir y no debe registrar ejecuciones inesperadas.
select to_regclass('public.schema_migrations') as tabla_migraciones;
select version, nombre, aplicado_en from public.schema_migrations order by aplicado_en;

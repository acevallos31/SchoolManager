-- Validacion 005: detectar ciclos sin institucion y periodos inconsistentes.
select id, nombre from public.ciclos_escolares where institucion_id is null;
select id, nombre, fecha_inicio, fecha_fin from public.periodos_matricula
where fecha_inicio > fecha_fin;

-- Seed sintético para benchmark PERF-01. Corre contra el esquema real del repo
-- (bootstrap + migraciones 001-016) sobre un Postgres local desechable.
-- Usa las funciones de dominio reales para garantizar filas válidas y ejercer
-- el mismo camino de escritura que la app. NO toca Supabase remoto.
--
-- Restricción clave: matriculas tiene UNIQUE (alumno_id, ciclo_id). Para lograr
-- >5000 matrículas con 2000 alumnos distribuimos (alumno,ciclo) sin repetir el
-- par: cada fila usa alumno_idx = (i-1)%2000 y ciclo_idx = (i-1)/2000 (+1), de
-- modo que cada par se usa 1 sola vez.

do $$
declare
  v_inst uuid;
  v_ciclos uuid[] := '{}'::uuid[];
  v_grade_list uuid[] := '{}'::uuid[];
  v_jornada uuid;
  v_g uuid;
  v_alumno uuid;
  v_seccion uuid;
  v_c_seccion uuid;
  v_periodo uuid;
  v_ciclo_idx int;
  v_i int;
  v_n int := 5600;
begin
  -- Institución
  insert into public.instituciones (nombre) values ('Bench Inst') returning id into v_inst;

  -- 4 ciclos (2024..2027), cada uno con su periodo.
  for v_i in 2024..2027 loop
    insert into public.ciclos_escolares (institucion_id, nombre, fecha_inicio, fecha_fin, activo)
    values (v_inst, 'Ciclo ' || v_i, make_date(v_i, 1, 15), make_date(v_i, 11, 30), true)
    returning id into v_c_seccion;
    v_ciclos := array_append(v_ciclos, v_c_seccion);
    insert into public.periodos_matricula (ciclo_id, nombre, fecha_inicio, fecha_fin, activo)
    values (v_c_seccion, 'Anual ' || v_i, make_date(v_i, 1, 15), make_date(v_i, 11, 30), true);
  end loop;

  -- Grados (8)
  for v_i in 1..8 loop
    insert into public.grados (nombre, orden) values ('Grado ' || v_i, v_i) returning id into v_g;
    v_grade_list := array_append(v_grade_list, v_g);
  end loop;

  -- Jornada
  insert into public.jornadas (nombre) values ('Matutina') returning id into v_jornada;

  -- 40 secciones: 10 por ciclo, repartidas entre grados.
  for v_i in 1..40 loop
    v_ciclo_idx := 1 + ((v_i - 1) / 10);
    v_c_seccion := v_ciclos[v_ciclo_idx];
    v_g := v_grade_list[1 + ((v_i - 1) % array_length(v_grade_list, 1))];
    insert into public.secciones (institucion_id, ciclo_id, grado_id, jornada_id, nombre)
    values (v_inst, v_c_seccion, v_g, v_jornada, 'Seccion ' || lpad(v_i::text, 2, '0'));
  end loop;

  -- 2000 alumnos con sus personas
  for v_i in 1..2000 loop
    perform public.crear_alumno_nueva_persona(
      v_inst, 'Nombre' || lpad(v_i::text, 5, '0'), 'Apellido' || (v_i % 100)
    );
  end loop;

  -- 5600 matrículas: (alumno_idx, ciclo_idx) únicos. alumno_idx=(i-1)%2000,
  -- ciclo_idx=(i-1)/2000+1 (1..3). Siempre elegimos la sección/periodo del ciclo.
  for v_i in 1..v_n loop
    v_ciclo_idx := (v_i - 1) / 2000 + 1;
    select id into v_alumno from public.alumnos
      where institucion_id = v_inst
      order by id
      limit 1 offset ((v_i - 1) % 2000);
    select id into v_seccion from public.secciones
      where ciclo_id = v_ciclos[v_ciclo_idx]
      order by created_at limit 1;
    select id into v_periodo from public.periodos_matricula
      where ciclo_id = v_ciclos[v_ciclo_idx]
      order by created_at limit 1;
    insert into public.matriculas (
      alumno_id, institucion_id, ciclo_id, seccion_id, periodo_matricula_id,
      fecha_matricula, estado, registrado_por
    ) values (
      v_alumno, v_inst, v_ciclos[v_ciclo_idx], v_seccion, v_periodo,
      make_date(2024, 1, 15) + (v_i % 300),
      case (v_i % 6) when 0 then 'pendiente' when 1 then 'activa' when 2 then 'finalizada'
           else 'finalizada' end,
      null
    );
  end loop;
  raise notice 'Seed OK: 2000 alumnos, 5600 matriculas, 40 secciones, 4 ciclos';
end;
$$;
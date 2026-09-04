# Benchmark de rendimiento (PERF-01 / PERF-03)

Harness reproducible sobre Postgres local desechable (Docker). NO toca Supabase
remoto ni datos reales: levanta un contenedor `postgres:16-alpine`, aplica el
esquema real del repo (bootstrap + migraciones 001-016 en orden) y siembra un
volumen representativo.

## Requisitos

- Docker (imagen `postgres:16-alpine` — el harness la pullea si falta).
- `psql` client (o se usa `docker exec` con psql del contenedor).

## Uso

```bash
cd perf/benchmark
./run_benchmark.sh            # levanta DB, aplica esquema, siembra, mide, EXPLAIN
```

Salida: `output/` con el log del benchmark y los resultados de EXPLAIN de las
consultas críticas.

## Datos sembrados (objetivo)

- 2.000 alumnos (personas + alumnos vinculados)
- 5.000+ matrículas históricas distribuidas en varios ciclos
- 30-50 secciones
- varios ciclos y períodos

## Qué mide

- Tiempo de listar alumnos (carga total vs paginado)
- Tiempo de listar matrículas (rango completo, filtros por alumno/ciclo/estado)
- Consultas de catálogos
- EXPLAIN (ANALYZE, BUFFERS) de las consultas críticas para decidir índices
- p50/p95/p99 de muestras repetidas

## Documentación

Los resultados baseline y la decisión de índices se consolidan en
`docs/decisiones/019-performance-concurrencia-stress.md`.

## Notas de entorno

- Los números son de una laptop/container local y **no** representan capacidad
  de producción. Documentan tendencias relativas antes/después de optimizar.
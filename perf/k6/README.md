# Stress testing (PERF-07)

Escenarios k6 reproducibles y **sin secretos**. El token/auth se inyecta desde
el entorno (`K6_TOKEN` o `PERF_TOKEN`) y NUNCA se versiona.

## Requisitos

- k6 (`https://grafana.com/docs/k6/` o `brew install k6`)
- Una API local/desplegable segura con datos de carga (ver `perf/benchmark`).

## Uso

```bash
cd perf/k6
export BASE_URL=http://localhost:5000
export K6_TOKEN=eyJ...   # JWT de un usuario con permisos académicos
k6 run --summary-export=result.json scenarios.ts
```

## Escenarios

Cada uno con escalones de usuarios concurrentes vía `ramping-vus`:
50 → 100 → 250 (→500 opcional si el entorno local lo soporta sin falsear).

| Escenario | Endpoint | Qué valida |
|-----------|----------|-----------|
| `listar_alumnos_paginado` | GET /api/alumnos?page&pageSize | paginación server-side |
| `buscar_alumno` | GET /api/alumnos?buscar= | búsqueda server-side |
| `listar_matriculas_paginado` | GET /api/matriculas?page&pageSize&estado | listado paginado |
| `crear_matricula` | POST /api/matriculas | escritura ACID |

## Limitaciones del entorno

- Los números de una laptop/Testcontainers **no** representan capacidad de
  producción.
- El token JWT debe ser emitido por el mismo issuer (Issuer/Audience) que valida
  la API.
- Para el escenario `crear_matricula` se necesita un contrato válido
  (alumnoId/seccionId/periodoMatriculaId) en la DB de carga.
- No afirmar capacidad productiva solo a partir de estos resultados.

## Métricas

Con `--summary-export` se capturan throughput, p50/p95/p99, tasa de errores.
`pg_stat_activity` y `pg_locks` se consultan en la DB local durante la prueba
para ver conexiones y locks (ver `monitor_db.sh`).
# Baseline y requisitos — Bloque PERF-01 (019)

## Baseline medido (PERF-01)

Postgres 16 local desechable (`perf/benchmark`), **2000 alumnos / 5600
matrículas / 40 secciones / 4 ciclos**, tras aplicar el esquema real
(bootstrap + migraciones 001-016). 5 corridas:

| Consulta | p50 | p95 | p99 |
|----------|-----|-----|-----|
| Q1 listar alumnos | 5.60 ms | 6.40 ms | 6.40 ms |
| Q2 listar matrículas (join completo) | 11.70 ms | 12.63 ms | 12.63 ms |
| Q3 filtro estado+ciclo+institución | 2.28 ms | 2.50 ms | 2.50 ms |
| Q4 filtro por alumno | 0.57 ms | 0.67 ms | 0.67 ms |
| Q5 catálogo grados | 0.20 ms | 0.26 ms | 0.26 ms |

**Lectura del baseline:**
- Las consultas planetadas (filtros) ya son rápidas y usan los índices
  existentes → ninguna mejora de base de datos produce una diferencia medible.
- **Q2 (lista completa con 5 joins) es el único punto de dolor a escala.**
  Descargar todas las matrículas para mostrar una página es quemar trabajo.
- La solución estructural es **paginación + búsqueda server-side** (PERF-02):
  la DB filtra/recorta antes de devolver. Esperado: cada página pasa de
  11.7 ms a ~1-2 ms (una página de 20 filas), y el payload de red cae órdenes
  de magnitud.

## Objetivos tras optimización (esperados)

| Listado | Antes (todo) | Después (página 20) |
|---------|--------------|---------------------|
| Matrículas | 11.7 ms / 5600 filas | ~1-2 ms / 20 filas |
| Alumnos (Supabase) | descarga completa | página de 20-100 vía `.range()` |

## Contrato de paginación (PERF-02)

**API .NET `GET /api/matriculas`** ahora acepta (opcional, compatible):

- `alumnoId`, `cicloId`, `estado`, `institucionId` → filtros server-side
- `page`, `pageSize` → si se envían, responde `PaginatedResult`:
  ```json
  { "items": [Matricula], "page": 1, "pageSize": 20,
    "totalItems": 5600, "totalPages": 280 }
  ```
  Si NO se envían → responde el arreglo plano (compatibilidad con clientes
  existentes).

**Frontend `MatriculaService.listarPaginado({...})`** expone el listado
paginado/filtrado; `listar()` se conserva sin cambios.

**Frontend alumnos (Supabase) `AlumnoService.buscarPaginado({...})`** filtra por
término/estado y recorta la página server-side con `.range()` + `count`,
evitando descargar todos los alumnos.
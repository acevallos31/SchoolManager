# HANDOFF — Bloque 019 (PERF-01): rendimiento, concurrencia, idempotencia y stress

## Estado: CERRADO para revisión — PR #30 Ready (sin mergear por regla)

Rama: `perf/escalabilidad-concurrencia-stress` · HEAD `604fedb` · PR **#30**
(OPEN, non-draft, mergeable CLEAN). **No mergear a main** (regla del usuario y
del bloque). No iniciar 018 Responsables hasta el cierre de este bloque.

## Qué se hizo

- **PERF-01/03 — Benchmark e índices.** Harness reproducible
  (`perf/benchmark/run_benchmark.sh`) sobre Postgres 16 local desechable:
  aplica el esquema real (bootstrap + migraciones 001-016), siembra 2000
  alumnos / 5600 matrículas / 40 secciones y corre `EXPLAIN (ANALYZE,
  BUFFERS)` + timing (p50/p95/p99 en `docs/decisiones/019-baseline-requisitos.md`).
  **Decisión: ningún índice nuevo** — los existentes ya cubren los filtros;
  el cuello (Q2 listado completo) se resuelve con paginación.
- **PERF-02 — Paginación server-side.** `GET /api/matriculas` acepta
  `page/pageSize/cicloId/estado` y devuelve `PaginatedResult`
  (`backend/.../DTOs/PaginatedResult.cs`); sin `page/pageSize` devuelve el
  arreglo plano (compatible). Frontend: `MatriculaService.listarPaginado()` y
  `AlumnoService.buscarPaginado()` (Supabase `.range()` + filtros server-side).
- **PERF-06 — Concurrencia.** `ConcurrenciaMatriculasTests`: cupo=1 con 8
  requests simultáneos → exactamente 1 creado / 7 rechazados (409), nunca
  excede cupo; mismo alumno/ciclo concurrente → 1 sola matrícula.
- **PERF-07 — Stress k6.** `perf/k6/scenarios.ts` sin secretos (token por
  env `K6_TOKEN`), escalones 50/100/250(/500 con `USERS_500=1`), thresholds;
  `monitor_db.sh` para conexiones/locks.
- **PERF-08 — Pooling.** `NpgsqlDataSource` singleton + pooling on es correcto;
  parámetros recomendados documentados, sin cambiar producción.

## Decisiones registradas (`docs/decisiones/019*`)

- **Caché (PERF-04):** descartada — sin beneficio medido (catálogos ~0.2 ms;
  el cuello era el listado completo, ya resuelto). Futuro: caché distribuida
  sólo para catálogos de baja mutación; nunca cachear cupo/estado/saldo.
- **Idempotencia (PERF-05):** defensa por restricción única `uq_matriculas_alumno_ciclo`
  + diseño de `Idempotency-Key` documentado (migración `idempotencia_operaciones`
  + insert `ON CONFLICT DO NOTHING` en la misma transacción) para PR 021+.
- **Índices (PERF-03):** ninguno nuevo.

## Calidad verificada (local)

- API IntegrationTests **33/33** (+ paginación y concurrencia)
- DB IntegrationTests **80/80**
- `ng test` **17 archivos / 73 tests** (+ listarPaginado)
- `dotnet build` Release 0w/0e · build prod OK · `git diff --check` limpio

## CI / Sonar

PR #30: **checks verdes** — Validar Compilación ✓, SonarCloud ✓ (tras cerrar
S2077 falso positivo con NOSONAR inline + S7688 `[[ ]]`), Vercel ✓.

## Métricas baseline (local, 2000/5600/40)

Q1 alumnos p50 5.6ms · Q2 matrículas full p50 11.7ms · Q3 filtro estado+ciclo+inst
p50 2.3ms · Q4 filtro alumno p50 0.6ms · Q5 catálogos p50 0.2ms.
Concurrencia: cupo=1 → 1/8 creada; mismo alumno/ciclo → 1/10 creada.

## Riesgos / deudas

- 17F sigue **parcial**: auditoría Supabase read-only (sin credenciales) y E2E
  real pendientes.
- k6 validado (compila/ejecuta); una corrida de carga real requiere API local
  + token; los números locales NO representan capacidad de producción.
- Frontend alumnos sigue leyendo por Supabase; la búsqueda paginada queda
  expuesta en el servicio pero la página aún no la usa (wiring UI pendiente).

## Siguiente recomendado

1. Merge del PR #30 (sujeto a aprobación del usuario; NO lo hace el agente).
2. `Idempotency-Key` (PERF-05) en un PR 021+.
3. Bloque **018 Responsables** tras el cierre de este bloque.
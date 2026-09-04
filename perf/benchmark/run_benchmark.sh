#!/usr/bin/env bash
# Benchmark PERF-01 + PERF-03: reproducible on a disposable local Postgres.
# Levanta postgres:16-alpine, aplica bootstrap + migraciones 001-016 reales,
# siembra 2000/5600/40 y mide consultas críticas + EXPLAIN (ANALYZE, BUFFERS).
# NO toca Supabase remoto ni datos reales.
#
# Uso: ./run_benchmark.sh   (requiere docker; opcional: TIMES=5 para más muestras)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BENCH_DIR="$ROOT/perf/benchmark"
OUT="$BENCH_DIR/output"
TIMES="${TIMES:-5}"
IMG="postgres:16-alpine"
NAME="sm_bench_019"

mkdir -p "$OUT"

echo "==> PostgreSQL desechable ($IMG)"
docker rm -f "$NAME" >/dev/null 2>&1 || true
docker run -d --name "$NAME" \
  -e POSTGRES_PASSWORD=postgres -e POSTGRES_USER=postgres -e POSTGRES_DB=schoolmanager \
  "$IMG" >/dev/null

# Esperar readiness
for i in $(seq 1 60); do
  if docker exec "$NAME" pg_isready -U postgres -d schoolmanager >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

PSQL() { docker exec -i "$NAME" psql -U postgres -d schoolmanager -v ON_ERROR_STOP=1 -q; }

echo "==> Bootstrap (Supabase + Legacy)"
PSQL < "$ROOT/tests/SchoolManager.Database.IntegrationTests/Infrastructure/SupabaseSecurityBootstrap.sql"
PSQL < "$ROOT/tests/SchoolManager.Database.IntegrationTests/Infrastructure/LegacySchemaBootstrap.sql"

echo "==> Usuario legacy requerido por 006 (mismo contrato que el fixture de tests)"
PSQL <<'SQLEOF'
insert into public.usuarios (usuario, rol)
values ('fixture-backfill-007', 'padre');
SQLEOF

echo "==> Migraciones 001-016 en orden"
for f in $(ls "$ROOT"/database/migrations/0*.sql | sort); do
  echo "    aplicando $(basename "$f")"
  PSQL < "$f"
done

echo "==> Seed (2000 alumnos / 5600 matrículas / 40 secciones)"
PSQL < "$BENCH_DIR/seed.sql"

echo "==> Conteos"
docker exec "$NAME" psql -U postgres -d schoolmanager -c \
  "select (select count(*) from public.alumnos) as alumnos, \
          (select count(*) from public.matriculas) as matriculas, \
          (select count(*) from public.secciones) as secciones, \
          (select count(*) from public.personas) as personas;" | tee "$OUT/counts.txt"

echo "==> Timing (${TIMES} corridas, p50/p95/p99 en Python)"
TIMING_LOG="$OUT/timing_raw.txt"
: > "$TIMING_LOG"
# queries.sql ya trae '\timing on' en la primera línea; psql lee el stdin.
for r in $(seq 1 "$TIMES"); do
  docker exec -i "$NAME" psql -U postgres -d schoolmanager \
    < "$BENCH_DIR/queries.sql" >> "$TIMING_LOG" 2>&1 || true
done

echo "==> EXPLAIN (ANALYZE, BUFFERS)"
EXPLAIN_LOG="$OUT/explain.txt"
: > "$EXPLAIN_LOG"
python3 - "$BENCH_DIR/queries.sql" "$EXPLAIN_LOG" "$NAME" <<'PYEOF'
import re, sys, pathlib, subprocess
src, outlog, name = sys.argv[1], pathlib.Path(sys.argv[2]), sys.argv[3]
text = pathlib.Path(src).read_text().replace('\\timing on', '').replace("\t", "  ")
# Split en python y ejecutar EXPLAIN una a una vía psql (stdin).
with outlog.open('a') as f:
    for i, chunk in enumerate(re.split(r';\s*\n', text), 1):
        stmt = chunk.strip()
        core = "\n".join(ln for ln in stmt.splitlines() if not ln.strip().startswith('--')).strip()
        if not core or not re.search(r'\b(select|with|explain)\b', core, re.I):
            continue
        label = (core.splitlines()[0].strip() if core.splitlines() else core)[:40]
        f.write(f"\n### Q{i} — {label}\n")
        r = subprocess.run(
            ['docker','exec','-i',name,'psql','-U','postgres','-d','schoolmanager'],
            input=f"explain (analyze, buffers) {core};\n",
            text=True, capture_output=True)
        f.write(r.stdout or "")
        if r.returncode != 0:
            f.write("EXIT=" + str(r.returncode) + "\n" + (r.stderr or ""))
print("grabado en", outlog)
PYEOF

echo "==> Listo. Resultados en $OUT/"
docker rm -f "$NAME" >/dev/null 2>&1 || true
echo "==> Contenedor eliminado (entorno desechable)."
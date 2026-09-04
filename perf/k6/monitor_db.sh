#!/usr/bin/env bash
# Monitorea conexiones, locks y buffer cache de Postgres mientras corre k6.
# Uso (mientras k6 corre en paralelo):
#   bash monitor_db.sh "host=localhost dbname=schoolmanager user=postgres" &
set -euo pipefail
CONN="${1:?Cadena de conexion psql: host=... dbname=... user=...}"
DURATION="${2:-120}"   # segundos de monitoreo
INTERVAL="${3:-5}"

echo "==> Monitoreando Postgres durante ${DURATION}s (muestras cada ${INTERVAL}s)"
END=$((SECONDS + DURATION))
while [[ $SECONDS -lt $END ]]; do
  echo "--- $(date +%T) ---"
  psql "$CONN" -c "select state, count(*) from pg_stat_activity \
    where datname is not null group by state order by count(*) desc;" 2>/dev/null || echo "(no pg_stat_activity)"
  psql "$CONN" -c "select locktype, mode, count(*) from pg_locks where not granted \
    group by locktype, mode order by count(*) desc limit 8;" 2>/dev/null || true
  psql "$CONN" -c "select * from pg_locks where granted = false and pid <> pg_backend_pid() limit 5;" 2>/dev/null || true
  sleep "$INTERVAL"
done
echo "==> Monitoreo finalizado."
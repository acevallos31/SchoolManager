#!/usr/bin/env python3
"""Levanta un staging E2E local y desechable para SchoolManager.

No acepta hosts remotos: usa exclusivamente Supabase local en 127.0.0.1.
El comando ``start`` elimina cualquier volumen local previo del proyecto,
levanta Auth/PostgREST/Postgres y aplica el baseline + migraciones 007 en
adelante, reproduciendo el historial vigente de SchoolManager.
"""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
BASELINE = ROOT / "database" / "baseline" / "001_schoolmanager_fase1a.sql"
MIGRATIONS = ROOT / "database" / "migrations"
LOCAL_DB_HOST = "127.0.0.1"
LOCAL_DB_PORT = "54322"
LOCAL_DB_NAME = "postgres"
LOCAL_DB_USER = "postgres"
LOCAL_DB_PASSWORD = "postgres"
LOCAL_SUPABASE_URL = "http://127.0.0.1:54321"
LOCAL_API_URL = "http://127.0.0.1:5000/api"
SUPABASE_EXCLUDES = (
    "studio,imgproxy,storage-api,realtime,edge-runtime,logflare,vector,"
    "supavisor,postgres-meta,mailpit"
)


def require_executable(name: str) -> None:
    if shutil.which(name) is None:
        raise RuntimeError(
            f"No se encontró '{name}' en PATH. Instálalo antes de iniciar el staging local."
        )


def run(
    args: list[str],
    *,
    check: bool = True,
    capture_output: bool = False,
    env: dict[str, str] | None = None,
) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        args,
        cwd=ROOT,
        check=check,
        text=True,
        capture_output=capture_output,
        env=env,
    )


def psql_args() -> list[str]:
    return [
        "psql",
        "--host",
        LOCAL_DB_HOST,
        "--port",
        LOCAL_DB_PORT,
        "--username",
        LOCAL_DB_USER,
        "--dbname",
        LOCAL_DB_NAME,
        "--set",
        "ON_ERROR_STOP=1",
    ]


def psql_env() -> dict[str, str]:
    return {**os.environ, "PGPASSWORD": LOCAL_DB_PASSWORD}


def active_migrations() -> list[Path]:
    paths: list[Path] = []
    for path in sorted(MIGRATIONS.glob("*.sql")):
        match = re.match(r"^(\d{3})_", path.name)
        if match and int(match.group(1)) >= 7:
            paths.append(path)
    return paths


def apply_sql(path: Path) -> None:
    print(f"Aplicando {path.relative_to(ROOT)}")
    run([*psql_args(), "--file", str(path)], env=psql_env())


def verify_schema() -> str:
    result = run(
        [
            *psql_args(),
            "--tuples-only",
            "--no-align",
            "--command",
            "select coalesce(max(version), '') from public.schema_migrations "
            "where version ~ '^[0-9]{3}$';",
        ],
        capture_output=True,
        env=psql_env(),
    )
    latest = result.stdout.strip()
    if not latest:
        raise RuntimeError("No se encontró ninguna migración numérica aplicada.")
    return latest


def parse_status_env(raw: str) -> dict[str, str]:
    values: dict[str, str] = {}
    for line in raw.splitlines():
        if "=" not in line:
            continue
        key, value = line.split("=", 1)
        values[key.strip()] = value.strip().strip('"').strip("'")
    return values


def print_runtime_environment() -> None:
    status = run(["supabase", "status", "-o", "env"], capture_output=True)
    values = parse_status_env(status.stdout)
    public_key = values.get("PUBLISHABLE_KEY") or values.get("ANON_KEY")

    print("\nStaging local listo.")
    print(f"E2E_SUPABASE_URL={LOCAL_SUPABASE_URL}")
    if public_key:
        print(f"E2E_SUPABASE_PUBLISHABLE_KEY={public_key}")
    else:
        print("E2E_SUPABASE_PUBLISHABLE_KEY=<copiar ANON_KEY de `supabase status -o env`>")
    print(f"E2E_API_URL={LOCAL_API_URL}")
    print("ASPNETCORE_ENVIRONMENT=Staging")
    print("ASPNETCORE_URLS=http://127.0.0.1:5000")


def start() -> None:
    require_executable("supabase")
    require_executable("psql")

    if not BASELINE.is_file():
        raise RuntimeError(f"No existe el baseline esperado: {BASELINE}")

    # El entorno es deliberadamente efímero: no conserva datos entre ciclos.
    run(["supabase", "stop", "--no-backup"], check=False)
    run(["supabase", "start", "--exclude", SUPABASE_EXCLUDES])

    apply_sql(BASELINE)
    migrations = active_migrations()
    if not migrations:
        raise RuntimeError("No se encontraron migraciones activas desde 007.")

    for migration in migrations:
        apply_sql(migration)

    latest = verify_schema()
    print(f"Esquema SchoolManager aplicado hasta migración {latest}.")
    print_runtime_environment()


def stop() -> None:
    require_executable("supabase")
    run(["supabase", "stop", "--no-backup"], check=False)
    print("Staging local detenido y volúmenes efímeros eliminados.")


def status() -> None:
    require_executable("supabase")
    run(["supabase", "status"])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("start", "stop", "status"))
    args = parser.parse_args()

    try:
        if args.action == "start":
            start()
        elif args.action == "stop":
            stop()
        else:
            status()
    except (RuntimeError, subprocess.CalledProcessError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

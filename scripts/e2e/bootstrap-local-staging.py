#!/usr/bin/env python3
"""Levanta un staging E2E local y desechable para SchoolManager.

No acepta hosts remotos: usa exclusivamente Supabase local. El comando
``start`` genera una copia efímera de las migraciones canónicas, elimina los
volúmenes locales previos y levanta Auth/PostgREST/Postgres desde cero.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import re
import shutil
import subprocess
import sys
from urllib.parse import unquote, urlparse

ROOT = Path(__file__).resolve().parents[2]
BASELINE = ROOT / "database" / "baseline" / "001_schoolmanager_fase1a.sql"
MIGRATIONS = ROOT / "database" / "migrations"
GENERATED_MIGRATIONS = ROOT / "supabase" / "migrations"
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
) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        args,
        cwd=ROOT,
        check=check,
        text=True,
        capture_output=capture_output,
    )


def active_migrations() -> list[Path]:
    paths: list[Path] = []
    for path in sorted(MIGRATIONS.glob("*.sql")):
        match = re.match(r"^(\d{3})_", path.name)
        if match and int(match.group(1)) >= 7:
            paths.append(path)
    return paths


def generated_name(version: int, source: Path) -> str:
    suffix = source.name.split("_", 1)[1]
    return f"{version:014d}_{suffix}"


def prepare_migrations() -> list[Path]:
    if not BASELINE.is_file():
        raise RuntimeError(f"No existe el baseline esperado: {BASELINE}")

    migrations = active_migrations()
    if not migrations:
        raise RuntimeError("No se encontraron migraciones activas desde 007.")

    shutil.rmtree(GENERATED_MIGRATIONS, ignore_errors=True)
    GENERATED_MIGRATIONS.mkdir(parents=True, exist_ok=True)

    shutil.copyfile(BASELINE, GENERATED_MIGRATIONS / generated_name(1, BASELINE))
    for source in migrations:
        version = int(source.name.split("_", 1)[0])
        shutil.copyfile(source, GENERATED_MIGRATIONS / generated_name(version, source))

    return migrations


def parse_status_env(raw: str) -> dict[str, str]:
    values: dict[str, str] = {}
    for line in raw.splitlines():
        if "=" not in line:
            continue
        key, value = line.split("=", 1)
        values[key.strip()] = value.strip().strip('"').strip("'")
    return values


def npgsql_connection_from_db_url(raw_url: str) -> str | None:
    parsed = urlparse(raw_url)
    if parsed.hostname not in {"127.0.0.1", "localhost"} or parsed.port != 54322:
        raise RuntimeError(
            "Supabase local devolvió una DB_URL fuera de 127.0.0.1:54322; se aborta por seguridad."
        )

    if not parsed.username or parsed.password is None or not parsed.path.strip("/"):
        return None

    return (
        f"Host={parsed.hostname};Port={parsed.port};Database={parsed.path.strip('/')};"
        f"Username={unquote(parsed.username)};Password={unquote(parsed.password)}"
    )


def print_runtime_environment() -> None:
    status = run(["supabase", "status", "-o", "env"], capture_output=True)
    values = parse_status_env(status.stdout)
    public_key = values.get("PUBLISHABLE_KEY") or values.get("ANON_KEY")
    db_url = values.get("DB_URL")

    print("\nStaging local listo.")
    print(f"E2E_SUPABASE_URL={LOCAL_SUPABASE_URL}")
    if public_key:
        print(f"E2E_SUPABASE_PUBLISHABLE_KEY={public_key}")
    else:
        print("E2E_SUPABASE_PUBLISHABLE_KEY=<copiar ANON_KEY de `supabase status -o env`>")
    print(f"E2E_API_URL={LOCAL_API_URL}")
    print("ASPNETCORE_ENVIRONMENT=Staging")
    print("ASPNETCORE_URLS=http://127.0.0.1:5000")

    if db_url:
        connection = npgsql_connection_from_db_url(db_url)
        if connection:
            print(f"ConnectionStrings__PostgreSQL={connection}")
            return

    print("ConnectionStrings__PostgreSQL=<copiar DB URL local de `supabase status` a formato Npgsql>")


def start() -> None:
    require_executable("supabase")
    migrations = prepare_migrations()

    # El entorno es deliberadamente efímero: no conserva datos entre ciclos.
    run(["supabase", "stop", "--no-backup"], check=False)
    run(["supabase", "start", "--exclude", SUPABASE_EXCLUDES])

    latest = migrations[-1].name.split("_", 1)[0]
    print(f"Baseline + migraciones 007-{latest} preparados y aplicados por Supabase CLI.")
    print_runtime_environment()


def stop() -> None:
    require_executable("supabase")
    run(["supabase", "stop", "--no-backup"], check=False)
    shutil.rmtree(GENERATED_MIGRATIONS, ignore_errors=True)
    print("Staging local detenido, volúmenes y migraciones generadas eliminados.")


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

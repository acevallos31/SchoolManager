#!/usr/bin/env python3
"""Levanta un staging E2E local y desechable para SchoolManager.

No acepta hosts remotos: usa exclusivamente Supabase local. El comando
``start`` genera una copia efímera de las migraciones canónicas, elimina los
volúmenes locales previos y levanta Auth/PostgREST/Postgres desde cero.
Los valores de runtime se guardan en ``.env.e2e.local`` (ignorado por Git) y
nunca se imprimen en consola.
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
ENV_FILE = ROOT / ".env.e2e.local"
LOCAL_SUPABASE_URL = "http://127.0.0.1:54321"
LOCAL_API_URL = "http://127.0.0.1:5000/api"
LOCAL_FRONTEND_URL = "http://127.0.0.1:4200"
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


def write_env_file(values: dict[str, str]) -> None:
    lines = [
        "# Generado por scripts/e2e/bootstrap-local-staging.py. NO versionar.",
        "# Contiene credenciales exclusivamente locales y efímeras.",
    ]
    lines.extend(f"{key}={value}" for key, value in values.items())
    ENV_FILE.write_text("\n".join(lines) + "\n", encoding="utf-8")
    try:
        ENV_FILE.chmod(0o600)
    except OSError:
        # Windows puede ignorar permisos POSIX; el archivo sigue fuera de Git.
        pass


def write_runtime_environment() -> None:
    status = run(["supabase", "status", "-o", "env"], capture_output=True)
    values = parse_status_env(status.stdout)
    public_key = values.get("PUBLISHABLE_KEY") or values.get("ANON_KEY")
    db_url = values.get("DB_URL")
    jwt_secret = values.get("JWT_SECRET")

    if not public_key:
        raise RuntimeError("Supabase local no devolvió PUBLISHABLE_KEY/ANON_KEY.")
    if not db_url:
        raise RuntimeError("Supabase local no devolvió DB_URL.")
    if not jwt_secret or len(jwt_secret.encode("utf-8")) < 32:
        raise RuntimeError("Supabase local no devolvió un JWT_SECRET seguro de al menos 32 bytes.")

    connection = npgsql_connection_from_db_url(db_url)
    if not connection:
        raise RuntimeError("No se pudo construir la cadena Npgsql del Supabase local.")

    write_env_file(
        {
            "E2E_LOCAL_STACK": "1",
            "E2E_STAGING": "1",
            "E2E_BASE_URL": LOCAL_FRONTEND_URL,
            "E2E_SUPABASE_URL": LOCAL_SUPABASE_URL,
            "E2E_SUPABASE_PUBLISHABLE_KEY": public_key,
            "E2E_API_URL": LOCAL_API_URL,
            "ASPNETCORE_ENVIRONMENT": "Staging",
            "ASPNETCORE_URLS": "http://127.0.0.1:5000",
            "JWT_SECRET": jwt_secret,
            "ConnectionStrings__PostgreSQL": connection,
        }
    )


def start() -> None:
    require_executable("supabase")
    migrations = prepare_migrations()
    ENV_FILE.unlink(missing_ok=True)

    # El entorno es deliberadamente efímero: no conserva datos entre ciclos.
    run(["supabase", "stop", "--no-backup"], check=False, capture_output=True)
    run(
        ["supabase", "start", "--exclude", SUPABASE_EXCLUDES],
        capture_output=True,
    )
    write_runtime_environment()

    latest = migrations[-1].name.split("_", 1)[0]
    print(f"Baseline + migraciones 007-{latest} aplicados en Supabase local.")
    print("Runtime E2E guardado en .env.e2e.local; sus valores no se muestran.")


def stop() -> None:
    require_executable("supabase")
    run(["supabase", "stop", "--no-backup"], check=False, capture_output=True)
    shutil.rmtree(GENERATED_MIGRATIONS, ignore_errors=True)
    ENV_FILE.unlink(missing_ok=True)
    print("Staging local detenido; datos, migraciones generadas y runtime local eliminados.")


def status() -> None:
    require_executable("supabase")
    result = run(["supabase", "status", "-o", "env"], capture_output=True)
    values = parse_status_env(result.stdout)
    api_url = values.get("API_URL", LOCAL_SUPABASE_URL)
    parsed = urlparse(api_url)
    if parsed.hostname not in {"127.0.0.1", "localhost"}:
        raise RuntimeError("El status recibido no corresponde al Supabase local esperado.")
    print("Supabase local está activo; no se muestran claves ni cadenas de conexión.")


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

#!/usr/bin/env python3
"""Crea el seed mínimo de autenticación para el staging E2E local.

El script solo opera contra Supabase local en 127.0.0.1/localhost:54321. Usa la
clave privilegiada efímera que entrega ``supabase status`` únicamente en memoria,
crea/actualiza dos identidades Auth de prueba y las vincula con usuarios/roles
de SchoolManager. Las contraseñas se generan aleatoriamente y se guardan solo en
``.env.e2e.local``, archivo ignorado por Git.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import secrets
import shutil
import subprocess
import sys
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlparse
from urllib.request import Request, urlopen
from uuid import NAMESPACE_URL, uuid5

ROOT = Path(__file__).resolve().parents[2]
ENV_FILE = ROOT / ".env.e2e.local"
LOCAL_SUPABASE_PORT = 54321
DEFAULT_SUPABASE_URL = "http://127.0.0.1:54321"
ADMIN_EMAIL = "admin.e2e@schoolmanager.test"
VIEWER_EMAIL = "consulta.e2e@schoolmanager.test"


def stable_uuid(name: str) -> str:
    return str(uuid5(NAMESPACE_URL, f"https://schoolmanager.local/e2e/{name}"))


IDS = {
    "institucion": stable_uuid("institucion"),
    "persona_admin": stable_uuid("persona/admin"),
    "persona_viewer": stable_uuid("persona/viewer"),
    "usuario_admin": stable_uuid("usuario/admin"),
    "usuario_viewer": stable_uuid("usuario/viewer"),
    "rol_admin": stable_uuid("rol/admin"),
    "rol_viewer": stable_uuid("rol/viewer"),
    "asignacion_admin": stable_uuid("asignacion/admin"),
    "asignacion_viewer": stable_uuid("asignacion/viewer"),
}


class LocalApi:
    def __init__(self, supabase_url: str, privileged_key: str) -> None:
        self.supabase_url = validate_local_supabase_url(supabase_url)
        self.privileged_key = privileged_key

    def request(
        self,
        method: str,
        path: str,
        *,
        query: dict[str, str] | None = None,
        payload: Any | None = None,
        prefer: str | None = None,
    ) -> Any:
        url = f"{self.supabase_url}{path}"
        if query:
            url += f"?{urlencode(query)}"

        body = None if payload is None else json.dumps(payload).encode("utf-8")
        headers = {
            "apikey": self.privileged_key,
            "Authorization": f"Bearer {self.privileged_key}",
            "Accept": "application/json",
        }
        if body is not None:
            headers["Content-Type"] = "application/json"
        if prefer:
            headers["Prefer"] = prefer

        request = Request(url, data=body, method=method, headers=headers)
        try:
            with urlopen(request, timeout=15) as response:
                raw = response.read()
        except HTTPError as error:
            endpoint = urlparse(url).path
            raise RuntimeError(
                f"{method} {endpoint} devolvió HTTP {error.code}; se aborta el seed."
            ) from error
        except URLError as error:
            raise RuntimeError(
                "No se pudo conectar con Supabase local. Ejecuta primero "
                "bootstrap-local-staging.py start."
            ) from error

        if not raw:
            return None
        return json.loads(raw.decode("utf-8"))

    def rest_get(self, table: str, **query: str) -> list[dict[str, Any]]:
        result = self.request("GET", f"/rest/v1/{table}", query=query)
        if not isinstance(result, list):
            raise RuntimeError(f"PostgREST devolvió una respuesta inesperada para {table}.")
        return result

    def upsert(self, table: str, rows: dict[str, Any] | list[dict[str, Any]]) -> None:
        self.request(
            "POST",
            f"/rest/v1/{table}",
            query={"on_conflict": "id"},
            payload=rows,
            prefer="resolution=merge-duplicates,return=minimal",
        )

    def delete(self, table: str, **query: str) -> None:
        self.request(
            "DELETE",
            f"/rest/v1/{table}",
            query=query,
            prefer="return=minimal",
        )


def require_executable(name: str) -> None:
    if shutil.which(name) is None:
        raise RuntimeError(f"No se encontró '{name}' en PATH.")


def run_status_env() -> dict[str, str]:
    require_executable("supabase")
    result = subprocess.run(
        ["supabase", "status", "-o", "env"],
        cwd=ROOT,
        check=True,
        text=True,
        capture_output=True,
    )
    values: dict[str, str] = {}
    for line in result.stdout.splitlines():
        if "=" not in line:
            continue
        key, value = line.split("=", 1)
        values[key.strip()] = value.strip().strip('"').strip("'")
    return values


def validate_local_supabase_url(raw_url: str) -> str:
    parsed = urlparse(raw_url.strip())
    if parsed.scheme != "http":
        raise RuntimeError("El seed local exige Supabase por http en localhost.")
    if parsed.hostname not in {"127.0.0.1", "localhost"}:
        raise RuntimeError("El seed local rechaza cualquier host remoto de Supabase.")
    if parsed.port != LOCAL_SUPABASE_PORT:
        raise RuntimeError(f"El seed local exige el puerto {LOCAL_SUPABASE_PORT}.")
    if parsed.username or parsed.password:
        raise RuntimeError("La URL local de Supabase no debe incluir credenciales.")
    if parsed.path not in {"", "/"}:
        raise RuntimeError("La URL local de Supabase no debe incluir una ruta adicional.")
    return f"http://{parsed.hostname}:{parsed.port}"


def load_env_file() -> dict[str, str]:
    if not ENV_FILE.is_file():
        raise RuntimeError(
            "No existe .env.e2e.local. Ejecuta primero bootstrap-local-staging.py start."
        )

    values: dict[str, str] = {}
    for raw_line in ENV_FILE.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        values[key.strip()] = value.strip()
    return values


def write_env_file(values: dict[str, str]) -> None:
    lines = [
        "# Generado para SchoolManager E2E local. NO versionar.",
        "# Contiene credenciales exclusivamente locales y efímeras.",
    ]
    preferred_order = [
        "E2E_LOCAL_STACK",
        "E2E_STAGING",
        "E2E_BASE_URL",
        "E2E_SUPABASE_URL",
        "E2E_SUPABASE_PUBLISHABLE_KEY",
        "E2E_API_URL",
        "E2E_INSTITUTION_ID",
        "E2E_USER_EMAIL",
        "E2E_USER_PASSWORD",
        "E2E_VIEWER_EMAIL",
        "E2E_VIEWER_PASSWORD",
        "ASPNETCORE_ENVIRONMENT",
        "ASPNETCORE_URLS",
        "ConnectionStrings__PostgreSQL",
    ]
    emitted: set[str] = set()
    for key in preferred_order:
        if key in values:
            lines.append(f"{key}={values[key]}")
            emitted.add(key)
    for key in sorted(set(values) - emitted):
        lines.append(f"{key}={values[key]}")

    ENV_FILE.write_text("\n".join(lines) + "\n", encoding="utf-8")
    try:
        ENV_FILE.chmod(0o600)
    except OSError:
        pass


def ensure_password(values: dict[str, str], key: str) -> str:
    current = values.get(key, "")
    if len(current) >= 20:
        return current
    generated = secrets.token_urlsafe(24)
    values[key] = generated
    return generated


def privileged_key(status: dict[str, str]) -> str:
    key = status.get("SERVICE_ROLE_KEY") or status.get("SECRET_KEY")
    if not key:
        raise RuntimeError("Supabase local no devolvió SERVICE_ROLE_KEY/SECRET_KEY.")
    return key


def list_auth_users(api: LocalApi) -> list[dict[str, Any]]:
    result = api.request(
        "GET",
        "/auth/v1/admin/users",
        query={"page": "1", "per_page": "1000"},
    )
    if isinstance(result, dict) and isinstance(result.get("users"), list):
        return result["users"]
    raise RuntimeError("Auth local devolvió una lista de usuarios inesperada.")


def ensure_auth_user(
    api: LocalApi,
    existing_users: list[dict[str, Any]],
    *,
    email: str,
    password: str,
    profile: str,
) -> str:
    existing = next(
        (
            user
            for user in existing_users
            if str(user.get("email", "")).lower() == email.lower()
        ),
        None,
    )

    payload = {
        "password": password,
        "user_metadata": {"schoolmanager_e2e": True, "profile": profile},
    }
    if existing:
        user_id = str(existing.get("id", ""))
        if not user_id:
            raise RuntimeError("Usuario Auth local existente sin id.")
        api.request("PUT", f"/auth/v1/admin/users/{user_id}", payload=payload)
        return user_id

    created = api.request(
        "POST",
        "/auth/v1/admin/users",
        payload={"email": email, "email_confirm": True, **payload},
    )
    if not isinstance(created, dict) or not created.get("id"):
        raise RuntimeError("Auth local no devolvió el id del usuario creado.")
    return str(created["id"])


def assert_single_mode_ready(api: LocalApi) -> None:
    config = api.rest_get(
        "configuracion_implementacion",
        select="multiples_instituciones",
        id="eq.1",
        limit="1",
    )
    if len(config) != 1 or config[0].get("multiples_instituciones") is not False:
        raise RuntimeError(
            "El seed 044C espera configuracion_implementacion en modo monoinstitución."
        )

    active = api.rest_get("instituciones", select="id", activo="eq.true")
    unexpected = [row for row in active if row.get("id") != IDS["institucion"]]
    if unexpected:
        raise RuntimeError(
            "El staging local contiene otra institución activa; reinícialo antes de sembrar 044C."
        )


def get_template(api: LocalApi, code: str) -> dict[str, Any]:
    rows = api.rest_get(
        "roles",
        select="id,plantilla_version",
        codigo=f"eq.{code}",
        tipo="eq.plantilla",
        institucion_id="is.null",
        activo="eq.true",
        limit="1",
    )
    if len(rows) != 1:
        raise RuntimeError(f"No se encontró la plantilla RBAC '{code}'.")
    return rows[0]


def sync_role_permissions(api: LocalApi, role_id: str, template_id: str) -> None:
    links = api.rest_get(
        "roles_permisos",
        select="permiso_id",
        rol_id=f"eq.{template_id}",
    )
    template_permission_ids = {str(row["permiso_id"]) for row in links}

    permissions = api.rest_get(
        "permisos",
        select="id,ambito,delegable,estado",
    )
    allowed_ids = sorted(
        str(permission["id"])
        for permission in permissions
        if str(permission["id"]) in template_permission_ids
        and permission.get("ambito") == "institucion"
        and permission.get("delegable") is True
        and permission.get("estado") == "vigente"
    )
    if not allowed_ids:
        raise RuntimeError("La plantilla RBAC no contiene permisos delegables vigentes.")

    api.delete("roles_permisos", rol_id=f"eq.{role_id}")
    api.request(
        "POST",
        "/rest/v1/roles_permisos",
        payload=[{"rol_id": role_id, "permiso_id": permission_id} for permission_id in allowed_ids],
        prefer="return=minimal",
    )


def seed_domain(api: LocalApi, admin_auth_id: str, viewer_auth_id: str) -> None:
    assert_single_mode_ready(api)

    api.upsert(
        "instituciones",
        {
            "id": IDS["institucion"],
            "nombre": "SchoolManager E2E",
            "nombre_corto": "E2E",
            "correo": ADMIN_EMAIL,
            "activo": True,
            "fecha_desactivacion": None,
            "motivo_desactivacion": None,
        },
    )

    admin_template = get_template(api, "school_admin")
    viewer_template = get_template(api, "demo_viewer")

    api.upsert(
        "roles",
        [
            {
                "id": IDS["rol_admin"],
                "codigo": "e2e_school_admin",
                "nombre": "Administrador E2E",
                "descripcion": "Rol local determinista para E2E autenticado.",
                "es_sistema": False,
                "activo": True,
                "institucion_id": IDS["institucion"],
                "tipo": "institucional",
                "protegido": False,
                "rol_base_id": str(admin_template["id"]),
                "plantilla_version": admin_template.get("plantilla_version"),
            },
            {
                "id": IDS["rol_viewer"],
                "codigo": "e2e_demo_viewer",
                "nombre": "Consulta E2E",
                "descripcion": "Rol local de solo lectura para pruebas negativas posteriores.",
                "es_sistema": False,
                "activo": True,
                "institucion_id": IDS["institucion"],
                "tipo": "institucional",
                "protegido": False,
                "rol_base_id": str(viewer_template["id"]),
                "plantilla_version": viewer_template.get("plantilla_version"),
            },
        ],
    )

    sync_role_permissions(api, IDS["rol_admin"], str(admin_template["id"]))
    sync_role_permissions(api, IDS["rol_viewer"], str(viewer_template["id"]))

    api.upsert(
        "personas",
        [
            {
                "id": IDS["persona_admin"],
                "nombres": "Administrador",
                "apellidos": "E2E",
                "correo": ADMIN_EMAIL,
                "estado": "activo",
                "fecha_desactivacion": None,
                "motivo_desactivacion": None,
            },
            {
                "id": IDS["persona_viewer"],
                "nombres": "Consulta",
                "apellidos": "E2E",
                "correo": VIEWER_EMAIL,
                "estado": "activo",
                "fecha_desactivacion": None,
                "motivo_desactivacion": None,
            },
        ],
    )

    api.upsert(
        "usuarios",
        [
            {
                "id": IDS["usuario_admin"],
                "persona_id": IDS["persona_admin"],
                "auth_user_id": admin_auth_id,
                "activo": True,
                "fecha_desactivacion": None,
                "motivo_desactivacion": None,
            },
            {
                "id": IDS["usuario_viewer"],
                "persona_id": IDS["persona_viewer"],
                "auth_user_id": viewer_auth_id,
                "activo": True,
                "fecha_desactivacion": None,
                "motivo_desactivacion": None,
            },
        ],
    )

    api.upsert(
        "usuarios_roles",
        [
            {
                "id": IDS["asignacion_admin"],
                "usuario_id": IDS["usuario_admin"],
                "rol_id": IDS["rol_admin"],
                "institucion_id": IDS["institucion"],
                "activo": True,
                "fecha_desactivacion": None,
                "motivo_desactivacion": None,
            },
            {
                "id": IDS["asignacion_viewer"],
                "usuario_id": IDS["usuario_viewer"],
                "rol_id": IDS["rol_viewer"],
                "institucion_id": IDS["institucion"],
                "activo": True,
                "fecha_desactivacion": None,
                "motivo_desactivacion": None,
            },
        ],
    )


def seed() -> None:
    env = load_env_file()
    supabase_url = validate_local_supabase_url(
        env.get("E2E_SUPABASE_URL", DEFAULT_SUPABASE_URL)
    )
    status = run_status_env()
    api = LocalApi(supabase_url, privileged_key(status))

    admin_password = ensure_password(env, "E2E_USER_PASSWORD")
    viewer_password = ensure_password(env, "E2E_VIEWER_PASSWORD")
    users = list_auth_users(api)
    admin_auth_id = ensure_auth_user(
        api,
        users,
        email=ADMIN_EMAIL,
        password=admin_password,
        profile="school_admin",
    )
    # Volvemos a consultar para mantener la operación idempotente incluso si el
    # segundo usuario ya existía antes de crear el primero.
    users = list_auth_users(api)
    viewer_auth_id = ensure_auth_user(
        api,
        users,
        email=VIEWER_EMAIL,
        password=viewer_password,
        profile="demo_viewer",
    )

    seed_domain(api, admin_auth_id, viewer_auth_id)

    env.update(
        {
            "E2E_INSTITUTION_ID": IDS["institucion"],
            "E2E_USER_EMAIL": ADMIN_EMAIL,
            "E2E_USER_PASSWORD": admin_password,
            "E2E_VIEWER_EMAIL": VIEWER_EMAIL,
            "E2E_VIEWER_PASSWORD": viewer_password,
        }
    )
    write_env_file(env)
    print("Seed 044C listo. Credenciales locales guardadas en .env.e2e.local (ocultas).")


def check() -> None:
    assert validate_local_supabase_url(DEFAULT_SUPABASE_URL) == DEFAULT_SUPABASE_URL
    if len(set(IDS.values())) != len(IDS):
        raise RuntimeError("Los UUID deterministas del seed no son únicos.")
    if not ADMIN_EMAIL.endswith(".test") or not VIEWER_EMAIL.endswith(".test"):
        raise RuntimeError("Los correos E2E deben permanecer en el TLD reservado .test.")
    print("Guardrails estáticos del seed 044C: OK.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", nargs="?", choices=("seed", "check"), default="seed")
    args = parser.parse_args()

    try:
        if args.action == "check":
            check()
        else:
            seed()
    except (RuntimeError, subprocess.CalledProcessError, json.JSONDecodeError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

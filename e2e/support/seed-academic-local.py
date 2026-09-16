#!/usr/bin/env python3
"""Extiende el seed E2E local con datos académicos y aislamiento institucional.

Solo opera después de ``scripts/e2e/seed-local-staging.py seed`` y reutiliza sus
guardrails: Supabase debe estar en loopback :54321 y la credencial privilegiada
permanece únicamente en memoria. No admite hosts remotos ni datos reales.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path
import sys
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
BASE_SEED_PATH = ROOT / "scripts" / "e2e" / "seed-local-staging.py"
ADMIN_B_EMAIL = "admin.b.e2e@schoolmanager.test"


def load_base_seed():
    spec = importlib.util.spec_from_file_location("schoolmanager_seed_044c", BASE_SEED_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError("No se pudo cargar el seed base 044C.")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


base = load_base_seed()


def sid(name: str) -> str:
    return base.stable_uuid(f"044d/{name}")


IDS = {
    "institucion_b": sid("institucion/b"),
    "persona_admin_b": sid("persona/admin-b"),
    "usuario_admin_b": sid("usuario/admin-b"),
    "rol_admin_b": sid("rol/admin-b"),
    "asignacion_admin_b": sid("asignacion/admin-b"),
    "ciclo_a": sid("academico/a/ciclo"),
    "periodo_a": sid("academico/a/periodo"),
    "grado_a": sid("academico/a/grado"),
    "jornada_a": sid("academico/a/jornada"),
    "seccion_a": sid("academico/a/seccion"),
    "persona_alumno_a": sid("academico/a/persona-alumno"),
    "alumno_a": sid("academico/a/alumno"),
    "matricula_a": sid("academico/a/matricula"),
    "ciclo_b": sid("academico/b/ciclo"),
    "periodo_b": sid("academico/b/periodo"),
    "grado_b": sid("academico/b/grado"),
    "jornada_b": sid("academico/b/jornada"),
    "seccion_b": sid("academico/b/seccion"),
    "persona_alumno_b": sid("academico/b/persona-alumno"),
    "alumno_b": sid("academico/b/alumno"),
    "matricula_b": sid("academico/b/matricula"),
}


def require_base_seed(api: Any) -> str:
    institucion_a = base.IDS["institucion"]
    rows = api.rest_get(
        "instituciones",
        select="id",
        id=f"eq.{institucion_a}",
        limit="1",
    )
    if len(rows) != 1:
        raise RuntimeError(
            "Falta la institución base 044C. Ejecuta primero seed-local-staging.py seed."
        )

    active = api.rest_get("instituciones", select="id", activo="eq.true")
    allowed = {institucion_a, IDS["institucion_b"]}
    unexpected = [str(row.get("id")) for row in active if str(row.get("id")) not in allowed]
    if unexpected:
        raise RuntimeError(
            "El staging local contiene instituciones activas ajenas al fixture 044D; reinícialo."
        )
    return institucion_a


def ensure_admin_b(api: Any, env: dict[str, str]) -> None:
    password = base.ensure_password(env, "E2E_ADMIN_B_PASSWORD")
    users = base.list_auth_users(api)
    auth_id = base.ensure_auth_user(
        api,
        users,
        email=ADMIN_B_EMAIL,
        password=password,
        profile="school_admin_b",
    )

    api.upsert(
        "instituciones",
        {
            "id": IDS["institucion_b"],
            "nombre": "SchoolManager E2E B",
            "nombre_corto": "E2E-B",
            "correo": ADMIN_B_EMAIL,
            "activo": True,
            "fecha_desactivacion": None,
            "motivo_desactivacion": None,
        },
    )

    template = base.get_template(api, "school_admin")
    api.upsert(
        "roles",
        {
            "id": IDS["rol_admin_b"],
            "codigo": "e2e_school_admin",
            "nombre": "Administrador E2E B",
            "descripcion": "Rol determinista de la segunda institución E2E.",
            "es_sistema": False,
            "activo": True,
            "institucion_id": IDS["institucion_b"],
            "tipo": "institucional",
            "protegido": False,
            "rol_base_id": str(template["id"]),
            "plantilla_version": template.get("plantilla_version"),
        },
    )
    base.sync_role_permissions(api, IDS["rol_admin_b"], str(template["id"]))

    api.upsert(
        "personas",
        {
            "id": IDS["persona_admin_b"],
            "nombres": "Administrador",
            "apellidos": "E2E B",
            "correo": ADMIN_B_EMAIL,
            "estado": "activo",
            "fecha_desactivacion": None,
            "motivo_desactivacion": None,
        },
    )
    api.upsert(
        "usuarios",
        {
            "id": IDS["usuario_admin_b"],
            "persona_id": IDS["persona_admin_b"],
            "auth_user_id": auth_id,
            "activo": True,
            "fecha_desactivacion": None,
            "motivo_desactivacion": None,
        },
    )
    api.upsert(
        "usuarios_roles",
        {
            "id": IDS["asignacion_admin_b"],
            "usuario_id": IDS["usuario_admin_b"],
            "rol_id": IDS["rol_admin_b"],
            "institucion_id": IDS["institucion_b"],
            "activo": True,
            "fecha_desactivacion": None,
            "motivo_desactivacion": None,
        },
    )

    env["E2E_ADMIN_B_EMAIL"] = ADMIN_B_EMAIL
    env["E2E_ADMIN_B_PASSWORD"] = password


def seed_academic_for(
    api: Any,
    *,
    suffix: str,
    institution_id: str,
    cycle_id: str,
    period_id: str,
    grade_id: str,
    shift_id: str,
    section_id: str,
    person_id: str,
    student_id: str,
    enrollment_id: str,
    registered_by: str,
) -> None:
    api.upsert(
        "ciclos_escolares",
        {
            "id": cycle_id,
            "institucion_id": institution_id,
            "nombre": f"Ciclo E2E 2026 {suffix}",
            "fecha_inicio": "2026-01-15",
            "fecha_fin": "2026-11-30",
            "activo": True,
            "fecha_desactivacion": None,
            "motivo_desactivacion": None,
        },
    )
    api.upsert(
        "periodos_matricula",
        {
            "id": period_id,
            "ciclo_id": cycle_id,
            "nombre": f"Ordinario E2E {suffix}",
            "tipo": "ordinario",
            "fecha_inicio": "2026-01-15",
            "fecha_fin": "2026-11-30",
            "activo": True,
        },
    )
    api.upsert(
        "grados",
        {
            "id": grade_id,
            "institucion_id": institution_id,
            "nombre": f"1er Grado E2E {suffix}",
            "orden": 1,
            "activo": True,
        },
    )
    api.upsert(
        "jornadas",
        {
            "id": shift_id,
            "institucion_id": institution_id,
            "nombre": f"Matutina E2E {suffix}",
            "activo": True,
        },
    )
    api.upsert(
        "secciones",
        {
            "id": section_id,
            "institucion_id": institution_id,
            "ciclo_id": cycle_id,
            "grado_id": grade_id,
            "jornada_id": shift_id,
            "nombre": f"Sección E2E {suffix}",
            "cupo": 30,
            "activo": True,
            "fecha_desactivacion": None,
            "motivo_desactivacion": None,
        },
    )
    api.upsert(
        "personas",
        {
            "id": person_id,
            "nombres": "Alumno",
            "apellidos": f"E2E {suffix}",
            "correo": f"alumno.{suffix.lower()}.e2e@schoolmanager.test",
            "estado": "activo",
            "fecha_desactivacion": None,
            "motivo_desactivacion": None,
        },
    )
    api.upsert(
        "alumnos",
        {
            "id": student_id,
            "persona_id": person_id,
            "institucion_id": institution_id,
            "codigo_interno": f"E2E-{suffix}-001",
            "fecha_nacimiento": "2015-05-10",
            "estado": "activo",
            "fecha_desactivacion": None,
            "motivo_desactivacion": None,
        },
    )
    api.upsert(
        "matriculas",
        {
            "id": enrollment_id,
            "alumno_id": student_id,
            "institucion_id": institution_id,
            "ciclo_id": cycle_id,
            "seccion_id": section_id,
            "periodo_matricula_id": period_id,
            "registrado_por": registered_by,
            "fecha_matricula": "2026-01-15",
            "estado": "activa",
            "fecha_anulacion": None,
            "motivo_anulacion": None,
            "plan_pago_id": None,
        },
    )


def seed() -> None:
    env = base.load_env_file()
    supabase_url = base.validate_local_supabase_url(
        env.get("E2E_SUPABASE_URL", base.DEFAULT_SUPABASE_URL)
    )
    status = base.run_status_env()
    api = base.LocalApi(supabase_url, base.privileged_key(status))

    institution_a = require_base_seed(api)
    ensure_admin_b(api, env)

    api.upsert(
        "configuracion_implementacion",
        {"id": 1, "multiples_instituciones": True},
    )

    seed_academic_for(
        api,
        suffix="A",
        institution_id=institution_a,
        cycle_id=IDS["ciclo_a"],
        period_id=IDS["periodo_a"],
        grade_id=IDS["grado_a"],
        shift_id=IDS["jornada_a"],
        section_id=IDS["seccion_a"],
        person_id=IDS["persona_alumno_a"],
        student_id=IDS["alumno_a"],
        enrollment_id=IDS["matricula_a"],
        registered_by=base.IDS["usuario_admin"],
    )
    seed_academic_for(
        api,
        suffix="B",
        institution_id=IDS["institucion_b"],
        cycle_id=IDS["ciclo_b"],
        period_id=IDS["periodo_b"],
        grade_id=IDS["grado_b"],
        shift_id=IDS["jornada_b"],
        section_id=IDS["seccion_b"],
        person_id=IDS["persona_alumno_b"],
        student_id=IDS["alumno_b"],
        enrollment_id=IDS["matricula_b"],
        registered_by=IDS["usuario_admin_b"],
    )

    env.update(
        {
            "E2E_INSTITUTION_A_ID": institution_a,
            "E2E_INSTITUTION_B_ID": IDS["institucion_b"],
            "E2E_STUDENT_A_ID": IDS["alumno_a"],
            "E2E_STUDENT_B_ID": IDS["alumno_b"],
            "E2E_STUDENT_A_NAME": "Alumno E2E A",
            "E2E_STUDENT_B_NAME": "Alumno E2E B",
        }
    )
    base.write_env_file(env)
    print("Seed 044D listo: dos instituciones y dataset académico local determinista.")


def check() -> None:
    all_ids = list(base.IDS.values()) + list(IDS.values())
    if len(all_ids) != len(set(all_ids)):
        raise RuntimeError("Los UUID deterministas 044C/044D deben ser únicos.")
    if not ADMIN_B_EMAIL.endswith(".test"):
        raise RuntimeError("El correo E2E debe permanecer en el TLD reservado .test.")
    print("Guardrails estáticos del seed académico 044D: OK.")


def main() -> int:
    action = sys.argv[1] if len(sys.argv) > 1 else "seed"
    if action not in {"seed", "check"}:
        print("Uso: seed-academic-local.py [seed|check]", file=sys.stderr)
        return 2
    try:
        check() if action == "check" else seed()
    except Exception as error:  # CI necesita fail-fast con mensaje sin secretos.
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

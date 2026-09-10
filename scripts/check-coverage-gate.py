#!/usr/bin/env python3
"""
Gate de cobertura contra regresiones reales (deuda tecnica #9).

Compara la cobertura de lineas generada por CI contra un baseline versionado en
docs/coverage-baseline.json. Falla SOLO si la cobertura actual cae por debajo de
baseline - TOLERANCIA. No impone un umbral global aspiracional sobre el historico.
"""
import argparse
import json
from pathlib import Path
import re
import sys

TOLERANCIA_PUNTOS = 1.0
WORKSPACE = Path.cwd().resolve()
BACKEND_PATTERN = "coverage-backend/**/coverage.cobertura.xml"
FRONTEND_PATTERN = "frontend/schoolmanager-frontend/coverage/**/lcov.info"


def _ruta_segura(path):
    """Resuelve y valida que un archivo permanezca dentro del workspace del CI."""
    candidato = Path(path)
    if candidato.is_absolute():
        raise SystemExit(f"[coverage-gate] no se permiten rutas absolutas: {path}")

    resuelta = (WORKSPACE / candidato).resolve()
    try:
        resuelta.relative_to(WORKSPACE)
    except ValueError as exc:
        raise SystemExit(f"[coverage-gate] ruta fuera del workspace: {path}") from exc

    if not resuelta.is_file():
        raise SystemExit(f"[coverage-gate] archivo no encontrado: {path}")
    return resuelta


def lineas_desde_lcov(path):
    ruta = _ruta_segura(path)
    lf = lh = 0
    for line in ruta.read_text(encoding="utf-8").splitlines():
        if line.startswith("LF:"):
            lf += int(line[3:])
        elif line.startswith("LH:"):
            lh += int(line[3:])
    if lf == 0:
        raise SystemExit(f"[coverage-gate] lcov sin lineas validas: {path}")
    return 100.0 * lh / lf


def lineas_backend(path, paquete):
    ruta = _ruta_segura(path)
    data = ruta.read_text(encoding="utf-8")
    m = re.search(
        r'<package name="' + re.escape(paquete) + r'"[\s\S]*?</package>', data
    )
    if not m:
        raise SystemExit(
            f"[coverage-gate] paquete '{paquete}' no encontrado en {path}"
        )
    cuerpo = m.group(0)
    lineas = re.findall(r'<line number="\d+" hits="(\d+)"', cuerpo)
    if not lineas:
        raise SystemExit(f"[coverage-gate] sin <line> en paquete {paquete}")
    total = len(lineas)
    hit = sum(1 for h in lineas if int(h) > 0)
    return 100.0 * hit / total


def resolver_ruta(patron):
    """Resuelve exclusivamente los dos artefactos de cobertura generados por CI."""
    if patron == BACKEND_PATTERN:
        coincidencias = list(WORKSPACE.glob("coverage-backend/**/coverage.cobertura.xml"))
    elif patron == FRONTEND_PATTERN:
        coincidencias = list(
            WORKSPACE.glob("frontend/schoolmanager-frontend/coverage/**/lcov.info")
        )
    else:
        raise SystemExit(f"[coverage-gate] patron de cobertura no permitido: {patron}")

    if not coincidencias:
        raise SystemExit(f"[coverage-gate] sin coincidencias para: {patron}")
    if len(coincidencias) > 1:
        raise SystemExit(
            f"[coverage-gate] multiples archivos coinciden con {patron}: {coincidencias}"
        )

    # Path.glob() devuelve una ruta absoluta porque WORKSPACE es absoluto.
    # La convertimos primero a relativa y reutilizamos la validacion central.
    relativa = coincidencias[0].resolve().relative_to(WORKSPACE)
    return str(_ruta_segura(relativa).relative_to(WORKSPACE))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--backend", required=True)
    ap.add_argument("--frontend", required=True)
    ap.add_argument("--baseline", default="docs/coverage-baseline.json")
    args = ap.parse_args()

    baseline_path = _ruta_segura(args.baseline)
    baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
    be_path = resolver_ruta(args.backend)
    fe_path = resolver_ruta(args.frontend)
    be_actual = lineas_backend(be_path, baseline["backend"]["paquete"])
    fe_actual = lineas_desde_lcov(fe_path)

    be_ref = baseline["backend"]["lineas"]
    fe_ref = baseline["frontend"]["lineas"]

    print(f"[coverage-gate] backend  actual={be_actual:.2f}%  baseline={be_ref:.2f}%")
    print(f"[coverage-gate] frontend actual={fe_actual:.2f}%  baseline={fe_ref:.2f}%")

    fallos = []
    for nombre, actual, ref in (
        ("backend", be_actual, be_ref),
        ("frontend", fe_actual, fe_ref),
    ):
        if actual < ref - TOLERANCIA_PUNTOS:
            fallos.append(
                f"{nombre}: {actual:.2f}% < baseline {ref:.2f}% - tolerancia {TOLERANCIA_PUNTOS}"
            )

    if fallos:
        print("[coverage-gate] REGRESION DETECTADA:")
        for fallo in fallos:
            print(f"  - {fallo}")
        sys.exit(1)

    print("[coverage-gate] OK: sin regresiones frente al baseline versionado.")


if __name__ == "__main__":
    main()

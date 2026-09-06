#!/usr/bin/env python3
"""
Gate de cobertura contra regresiones reales (deuda tecnica #9).

Compara la cobertura de lineas generada por CI contra un baseline versionado en
docs/coverage-baseline.json. Fallo SOLO si la cobertura actual cae por debajo de
baseline - TOLERANCIA. No impone un umbral global aspiracional sobre el historico
(backend ~79.6%, frontend ~64%), por lo que no exige subir la cobertura: solo
protege contra regresiones reales.

Uso:
  python3 scripts/check-coverage-gate.py \
      --backend <coverage.cobertura.xml> \
      --frontend <lcov.info>

New Code / diff coverage real requiere SonarCloud (SONAR_TOKEN), no simulado aqui:
#9 se deja como PARCIAL con este gate local/CI razonable. Ver docs/technical-debt.md.
"""
import argparse
import glob
import json
import os
import re
import sys

TOLERANCIA_PUNTOS = 1.0  # margen para absorber fluctuaciones de medicion no regresiones reales


def lineas_desde_lcov(path):
    lf = lh = 0
    for line in open(path, encoding="utf-8"):
        if line.startswith("LF:"):
            lf += int(line[3:])
        elif line.startswith("LH:"):
            lh += int(line[3:])
    if lf == 0:
        raise SystemExit(f"[coverage-gate] lcov sin lineas validas: {path}")
    return 100.0 * lh / lf


def lineas_backend(path, paquete):
    data = open(path, encoding="utf-8").read()
    # Filtrar solo el paquete productivo (excluye proyectos de test).
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
    """Resuelve un patron de ruta (posiblemente con **/glob) a un archivo unico."""
    if "*" in patron:
        coincidencias = glob.glob(patron, recursive=True)
        if not coincidencias:
            raise SystemExit(f"[coverage-gate] sin coincidencias para: {patron}")
        if len(coincidencias) > 1:
            raise SystemExit(
                f"[coverage-gate] multiples archivos coinciden con {patron}: {coincidencias}"
            )
        return coincidencias[0]
    return patron


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--backend", required=True)
    ap.add_argument("--frontend", required=True)
    ap.add_argument("--baseline", default="docs/coverage-baseline.json")
    args = ap.parse_args()

    baseline = json.load(open(args.baseline, encoding="utf-8"))
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
        for f in fallos:
            print(f"  - {f}")
        sys.exit(1)

    print("[coverage-gate] OK: sin regresiones frente al baseline versionado.")
    sys.exit(0)


if __name__ == "__main__":
    main()

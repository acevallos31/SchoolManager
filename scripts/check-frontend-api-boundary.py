"""Guard lexical de deuda #10; auth.ts es la única excepción productiva."""

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "frontend/schoolmanager-frontend/src"
AUTH = SOURCE / "app/core/services/auth.ts"
PATTERN = re.compile(r"SUPABASE_CLIENT|createClient|@supabase/|\.\s*(?:from|rpc)\s*\(")
ARRAY_FROM = re.compile(r"\bArray\s*\.\s*from\s*\(")


def main() -> int:
    violations = []
    for path in sorted(SOURCE.rglob("*.ts")):
        if path == AUTH or path.name.endswith((".spec.ts", ".test.ts")):
            continue
        # Array.from es JavaScript nativo, no una llamada a la Data API.
        source = ARRAY_FROM.sub("ArrayFrom(", path.read_text(encoding="utf-8-sig"))
        for match in PATTERN.finditer(source):
            line = source.count("\n", 0, match.start()) + 1
            violations.append(f"{path.relative_to(ROOT).as_posix()}:{line}: {match.group()}")
    if violations:
        print("Accesos directos fuera de auth.ts:")
        print("\n".join(violations))
        return 1
    print("Frontera API: 0 coincidencias fuera de auth.ts (tests excluidos; Array.from nativo).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

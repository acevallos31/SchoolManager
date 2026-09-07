# Handoff — Bloque 029B: SonarCloud C# completo (análisis estático real del backend)

**Rama:** `feature/calidad-sonar-e2e-029` · **Base:** `origin/main`
**Fecha:** 2026-09-07 · **PR:** #52 contra `main` (sin merge)
**Estado final: RESUELTO (residual del 029 cerrado)** — análisis estático **C# del
backend** real en CI + frontend TS + coberturas, Quality Gate verde y representativo.
E2E autenticado sigue pendiente de staging (acción humana #2, fuera de este bloque).

## Objetivo
Cerrar el residual de la deuda #7/#9 dejado por el 029: el scanner CLI genérico
(`SonarSource/sonarqube-scan-action@v8.2.1`) analizaba frontend TS y cobertura, pero
**NO hacía análisis estático de los `.cs`** del backend (warning `C# files which
cannot be analyzed with the scanner you are using... requires SonarScanner for .NET
5.x or higher`). El 029B migra el job a **SonarScanner for .NET** sin perder el
análisis TypeScript ni las coberturas C#/LCOV ya importadas.

## Qué se hizo (commits `11b5146` → `6e3299a`)
En el job `sonarcloud` de `.github/workflows/deploy.yml`:

1. **Migración a SonarScanner for .NET (`dotnet-sonarscanner`, tool v11.3.0)** con
   flujo `begin → dotnet build (proyectos de tests, genera Cobertura) → end`,
   sustituyendo la action genérica.
2. **Se ELIMINÓ `sonar-project.properties`.** El scanner .NET NO lo lee (da error si
   existe en el `begin`). Todas las propiedades pasan como `/d:` en el `begin`:
   `sonar.organization`, `sonar.projectKey`, `sonar.host.url`,
   `sonar.scanner.scanAll=true` (clave: conserva el análisis TS/frontend standalone),
   `sonar.sources`, `sonar.tests`, `sonar.cs.opencover.reportsPaths` (cobertura
   backend Cobertura), `sonar.javascript.lcov.reportPaths` (frontend LCOV),
   `sonar.exclusions` (ahora **incluye `e2e/**`**, además de node_modules/dist/bin/
   obj/coverage/lcov/spec/test/etc.).
3. Se fijó `set -f` en el paso `begin` para que los patrones `**` de `sonar.exclusions`
   se pasen **literales** al scanner (sin expansión por el shell).
4. **Quality Gate explícito:** se añadió el paso
   `SonarSource/sonarqube-quality-gate-action` pineado por **SHA**
   `7a5fffe8e523c40e0c740b6bc2712ab503e52efa` (= v1.2.1) tras el `end`. Lee
   `.sonarqube/out/.sonar/report-task.txt` (ruta específica del scanner .NET), consulta
   el QG del PR y **falla el job si NO es verde**. La app de SonarCloud **no crea check
   QG con el scanner CLI**, así que este paso es necesario para el anti falso-verde
   extendido al Quality Gate.
5. `dotnet tool install` del scanner .NET en el runner (global-tool) antes del `begin`.

## Verificación (anti falso-verde: confirmado por LOG, no por conclusión)
Run verde **34160443496** (head `6e3299a`). Del log del job `sonarcloud`:
- **Warning fantasma AUSENTE:** `grep -c "cannot be analyzed with the scanner"` → **0**.
- **`.cs` realmente procesados:** `Indexing files of module 'SchoolManager.API'`,
  `'SchoolManager.API.IntegrationTests'`, `'SchoolManager.Database.IntegrationTests'`.
- **Frontend TS conservado:** `Creating TypeScript(6.0.3) program ...tsconfig.json`,
  `Analyzing 59 file(s) from tsconfig ...` (sensor JavaScript/TypeScript/CSS).
- **Cobertura backend:** `Parsing the Cobertura report ...coverage.cobertura.xml`;
  `Coverage Report Statistics: 39 files, 15 main files, 15 main files with coverage`.
- **Cobertura frontend:** `Analysing .../coverage/schoolmanager-frontend/lcov.info`.
- **Quality Gate:** `✔ Quality Gate has PASSED` · `ANALYSIS SUCCESSFUL`
  (dashboard `https://sonarcloud.io/dashboard?id=SchoolManager&pullRequest=52`).

## El QG se volvió genuinamente representativo (y el anti falso-verde funcionó)
Al analizar C# por primera vez, el QG del PR #52 **falló en rojo** por 2 vulnerabilidades
en **código nuevo del propio `deploy.yml`**, invisibles con el scanner genérico
(verificado vía `issues/search`):
- `githubactions:S8482` **BLOCKER** (línea del paso diagnóstico `curl | python3`) —
  *"Avoid executing downloaded artifacts directly without verification."*
- `githubactions:S7637` **MAJOR** — *"Use full commit SHA hash for this dependency"*
  (la action QG estaba en tag `@v1.2.1`).

Corrección (commits `143eb35`/`6e3299a`): se **eliminó el paso diagnóstico temporal**
(redundante con la action QG) y se **pineó la action QG por SHA**. Con eso el QG pasó
verde. No había vulnerabilidades en el código C# del backend: los `.cs` están limpios.
Este ciclo rojo→verde es la prueba de que el QG ahora representa de verdad el backend.

> Nota de debugging: el `issues/search` de SonarCloud devuelve HTTP 400 si `types` incluye
> `SECURITY_HOTSPOT` (no es valor válido para ese parámetro); solo acepta
> `VULNERABILITY`, `BUG`, `CODE_SMELL`.

## Estado de la deuda técnica (docs actualizados en `6e3299a` o siguiente commit doc)
- **Deuda #7** → **RESUELTA** (análisis estático completo C# + TS + coberturas, QG verde).
- **Deuda #9** → **ACTUALIZADA/RESUELTA** (QG representativo del backend; New Code de
  SonarCloud ya se calcula sobre backend + frontend).
- `docs/AI_CONTEXT.md`: bloque 029+029B → **CERRADO (verde real)**.
- `docs/technical-debt.md`: #7 RESUELTA, #9 ACTUALIZADA/RESUELTA.

## Riesgos residuales
1. **Pines de actions en tags:** otros `uses:` del workflow (`@v4`) no están por SHA.
   No fueron señalados como vulnerabilidades de código nuevo (no están en el diff del
   PR), pero conviene pinearlos por SHA en un hardening futuro.
2. **Pin `@v8.2.1` de `sonarqube-scan-action` quedó obsoleto** al migrar a SonarScanner
   for .NET; revisar/retirar la referencia si no se usa en otro job.
3. **E2E autenticado pendiente** de staging (acción humana #2, `docs/ci/e2e-auth-setup.md`).
4. **Automatic Analysis** en el panel de SonarCloud puede generar análisis duplicados
   (paso manual del mantenedor).

## Acciones humanas requeridas
1. **SONAR_TOKEN — RESUELTA** (029).
2. **Staging E2E — PENDIENTE** (029; ver `docs/ci/e2e-auth-setup.md`).
3. **Umbral de New Code en SonarCloud — RECOMENDADO** (paso manual del mantenedor):
   configurar QG de New Code ≥ 80% backend / ≥ 70% frontend, ahora que la métrica es real.

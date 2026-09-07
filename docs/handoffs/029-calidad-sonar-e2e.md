# Handoff — Bloque 029: Calidad, Sonar real y E2E autenticado

**Rama:** `feature/calidad-sonar-e2e-029` · **Base:** `origin/main`
**Fecha:** 2026-09-07 · **PR:** #52 contra `main` (sin merge)
**Estado final:** SonarCloud real en verde · E2E autenticado pendiente de staging (acción humana #2)

## Resumen ejecutivo
- **SonarCloud real restaurado y en verde.** El job `sonarcloud` ya ejecuta un
  análisis real (cobertura backend + frontend importadas, Quality Gate del PR
  verde). Run validado: **34154637093** (HEAD técnico previo al cierre docs:
  `e76cedc`).
- **Diagnóstico y causa raíz documentados** (ver «Diagnóstico» abajo): dos
  capas — el CLI manual moría con `exit 8` sin salida útil, y la causa raíz
  real eran **wildcards en `sonar.sources`/`sonar.tests`** rechazadas por
  SonarScanner 8.x.
- **`SONAR_TOKEN` configurado y válido.** El secret existe en el repo, GitHub
  lo inyecta correctamente; validado contra `/api/authentication/validate`
  (`valid=true`). Acción humana #1 **resuelta**.
- **Harness E2E (Playwright)** creado en `e2e/`, aislado del frontend, con
  guardrail anti-producción. **Smoke E2E ejecutado en local: 3/3 passed.**
- **E2E autenticado** preparado (`e2e/tests/auth.spec.ts`) pero **no ejecutable**
  aún: exige staging controlado + credenciales de prueba (acción humana #2) —
  ver `docs/ci/e2e-auth-setup.md`.

## Diagnóstico (por qué el job sonarcloud no analizaba de verdad)
1. **CLI manual `sonar-scanner-cli-7.1.0.12063` (descargado con wget+unzip en
   deploy.yml):** moría con **exit 8 en ~0.6 s sin imprimir nada** del scanner
   (ni banner). Con `SONAR_TOKEN` ya inyectado (`SONAR_TOKEN: ***` en logs) y
   Java 17 correcto, se descartaron red/credenciales/versión de Java: era un
   **fallo de arranque del wrapper** del CLI 7.1 sin diagnóstico útil en CI.
   Reproducción local imposible (host sin JVM; `binaries.sonarsource.com`
   devuelve HTTP 403 desde esta máquina).
2. **Fix del arranque:** se sustituyó la descarga manual por la **action
   oficial `SonarSource/sonarqube-scan-action@v8.2.1`** (versión estable actual
   a 2026-07-15; auto-aprovisiona su JRE y su sonar-scanner). Se eliminó
   `actions/setup-java@v4` del job (la action ya no lo necesita; además se
   zanja la deprecación de Node 20 de esa action).
3. **Causa raíz real (descubierta con la action oficial):** `sonar.sources` y
   `sonar.tests` usaban comodines (`**`, `*`). SonarScanner 8.x **prohíbe
   wildcards** en esas dos propiedades y falla en configuración con exit code 3:
   ```
   ERROR Invalid value of sonar.tests for SchoolManager
   ERROR Wildcards ** and * are not supported in "sonar.sources" and "sonar.tests".
   ```
4. **Fix del config:** `sonar.sources` y `sonar.tests` pasaron a **solo
   directorios** (sin `**` ni `*`):
   - `sonar.sources=backend,frontend/schoolmanager-frontend/src`
   - `sonar.tests=tests`
   - Los `.spec.ts` colocalizados bajo `src/` y los artefactos/build/coverage
     se excluyen vía `sonar.exclusions` (que sí admite wildcards). La cobertura
     es correcta: el LCOV de vitest solo mide código productivo.

## Cambios y verificación
| Ítem | Estado |
| --- | --- |
| Guard anti falso-verde (deploy.yml) | ✅ activo (job FALLA si falta SONAR_TOKEN) |
| Migración a `SonarSource/sonarqube-scan-action@v8.2.1` | ✅ (scanner 8.1.0.6389 auto-aprovisionado) |
| `actions/setup-java@v4` eliminado del job sonarcloud | ✅ |
| `sonar-project.properties`: sources/tests solo directorios (commit `e76cedc`) | ✅ |
| `docs/ci/sonarcloud-token-setup.md` | ✅ actualizado (estado real + regla de directorios) |
| `docs/ci/e2e-auth-setup.md` | ✅ creado (77 líneas) |
| `e2e/` harness (package.json, playwright.config, specs, .gitignore) | ✅ |
| Smoke E2E local (`E2E_BASE_URL=http://localhost:4200 npm test`) | ✅ **3/3 passed** |
| Auth E2E (staging gated) | ⏸ 2 skipped (honesto, sin staging) |
| `npx ng build` | ✅ 8.2s OK |
| Unit tests `ng test --watch=false` | ✅ (ver número abajo) |
| Prompt `docs/agent-prompts/029-calidad-sonar-e2e.md` | ✅ creado |

## Resultado final de Sonar / cobertura / Quality Gate
Run validado **34154637093** (head `e76cedc`, 2026-09-07):

- **Análisis SonarCloud (CI Analysis): pass** (1m11s) — análisis real, no skip.
- **SonarCloud Code Analysis: pass** (Quality Gate del PR) —
  `https://sonarcloud.io/dashboard?id=SchoolManager&pullRequest=52`.
- Log del scanner confirma import real de cobertura:
  - Cobertura C#: `39 files, 15 main files, 15 main files with coverage, 24 test files`.
  - Cobertura TS (LCOV): `Analysing [.../coverage/schoolmanager-frontend/lcov.info]`.
  - `ANALYSIS SUCCESSFUL` · `EXECUTION SUCCESS`.
- Cobertura local medida (2026-09-06): backend **81.54%** líneas, frontend
  **64.86%** líneas (baseline versionado en `docs/coverage-baseline.json`;
  gate de regresión local activo).

## Smoke E2E ejecutado (real, local, sin datos ni producción)
```
Running 5 tests
✓ login público: el SPA arranca y se renderiza el formulario de acceso (939ms)
✓ login público: campos vacíos muestran aviso accesible y no navegan (1.1s)
✓ login público: ruta protegida redirige a /login sin sesión (710ms)
- (skipped) login correcto llega al dashboard ... (necesita E2E_STAGING)
- (skipped) logout devuelve al login ...          (necesita E2E_STAGING)
3 passed, 2 skipped
```

## Hallazgo de calidad (del E2E)
El formulario de login **no valida formato de email client-side**. Angular añade
`novalidate` a los formularios, por lo que la validación HTML5 (`type=email`,
`required`) NO bloquea el submit. `login.ts` solo comprueba campos no vacíos
(«Ingresa tu correo y contrasena para continuar.») y delega el rechazo de un
email malformado al proveedor. **Mejora sugerida (fuera de alcance de este
bloque):** añadir validación de formato email en `login.ts` + test unitario.
No es un riesgo de seguridad (Supabase valida credenciales), solo UX/feedback.

## Riesgos residuales
1. **E2E autenticado pendiente** de staging provisionado + credenciales de
   prueba (bloqueador recurrente documentado desde el 024; sin entorno
   staging/preview ni datos) — acción humana #2, ver `docs/ci/e2e-auth-setup.md`.
2. Revisión visual en navegador de las páginas sigue pendiente (mismo bloqueador).
3. **Automatic Analysis** en el panel de SonarCloud: si no se desactivó, puede
   generar análisis duplicados junto al CI Analysis (paso manual del
   mantenedor; no automatizable vía repo).
4. La acción oficial se fijó al pin `v8.2.1`; al publicarse versiones nuevas
   conviene revisar el pin (no es un tag móvil a propósito, para evitar la
   regresión histórica de `@v5`/v5.3.1).

## Acciones humanas requeridas
1. **SONAR_TOKEN — RESUELTA.** Ya configurado y válido. Ver
   `docs/ci/sonarcloud-token-setup.md` (procedimiento de rotación + estado).
2. **Staging E2E — PENDIENTE** (ver `docs/ci/e2e-auth-setup.md`): proyecto
   Supabase de staging + backend + `environment.staging.ts` + usuario de prueba.

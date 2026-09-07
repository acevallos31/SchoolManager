# Handoff — Bloque 029: Calidad, Sonar real y E2E autenticado

**Rama:** `feature/calidad-sonar-e2e-029` · **Base:** `origin/main`
**Fecha:** 2026-09-07 · **PR:** creado contra `main` (sin merge)

## Resumen ejecutivo
- **SonarCloud falso-verde corregido** en `.github/workflows/deploy.yml`: el job
  `sonarcloud` ahora **falla** si `sonar-scanner` fue omitido por falta de
  `SONAR_TOKEN` (guard con `if: steps.sonar-scan.outcome == 'skipped'`). Un job
  interno ya no puede quedar verde sin haber subido análisis real.
- **SONAR_TOKEN no configurado** (verificado por nombres: solo hay secrets de
  RENDER y VERCEL; la org `acevallos31` no tiene secret store). Acción humana
  pendiente — ver `docs/ci/sonarcloud-token-setup.md`.
- **Harness E2E (Playwright)** creado en `e2e/`, aislado del frontend, con
  guardrail anti-producción. **Smoke E2E ejecutado en local: 3/3 passed.**
- **E2E autenticado** preparado (`e2e/tests/auth.spec.ts`) pero **no ejecutable**
  aún: exige staging controlado + credenciales de prueba (acción humana) — ver
  `docs/ci/e2e-auth-setup.md`.

## Cambios y verificación
| Ítem | Estado |
| --- | --- |
| Guard SonarCloud (deploy.yml) | ✅ aplicado, YAML consistente |
| `sonar-project.properties` (comentario actualizado) | ✅ |
| `docs/ci/sonarcloud-token-setup.md` | ✅ creado |
| `docs/ci/e2e-auth-setup.md` | ✅ creado (77 líneas) |
| `e2e/` harness (package.json, playwright.config, specs, .gitignore) | ✅ |
| Smoke E2E local (`E2E_BASE_URL=http://localhost:4200 npm test`) | ✅ **3/3 passed** |
| Auth E2E (staging gated) | ⏸ 2 skipped (honesto, sin staging) |
| `npx ng build` | ✅ 8.2s OK |
| Unit tests `ng test --watch=false` | ✅ (ver número abajo) |
| Prompt `docs/agent-prompts/029-calidad-sonar-e2e.md` | ✅ creado |

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
1. **SONAR_TOKEN sin configurar** → el check `sonarcloud` quedará **en rojo**
   en PRs hasta que se configure (a propósito, anti falso-verde). No bloquea
   despliegues (`deploy` solo depende de `validate-code`).
2. **E2E autenticado pendiente** de staging provisionado + credenciales de
   prueba (bloqueador recurrente documentado desde el 024; sin entorno
   staging/preview ni datos).
3. Revisión visual en navegador de las páginas sigue pendiente (mismo bloqueador).

## Acciones humanas requeridas
1. **SONAR_TOKEN** (ver `docs/ci/sonarcloud-token-setup.md`): generar un
   *analysis token* en la cuenta SonarCloud y
   `gh secret set SONAR_TOKEN` (repo u org). El workflow lo lee vía
   `secrets.SONAR_TOKEN`; nunca commitear el valor.
2. **Staging E2E** (ver `docs/ci/e2e-auth-setup.md`): proyecto Supabase de
   staging + backend + `environment.staging.ts` + usuario de prueba.

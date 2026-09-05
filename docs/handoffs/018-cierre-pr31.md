# Handoff 018 — Cierre del Bloque Responsables (PR #31)

- **Rama:** `feature/responsables-fase-018` → `main`
- **PR:** https://github.com/acevallos31/SchoolManager/pull/31 (sin mergear)
- **SHA final:** `3fe7baa`
- **Merge con main:** `3fe7baa` (Merge remote-tracking 'origin/main') — resuelto conflicto
  add/add en `.sonarcloud.properties` (ambos lados tenían la misma exclusion; se conservó
  la versión de main, autoritativa).
- **Commits del cierre:**
  - `b6decce` docs: agregar prompt smart para cierre 018 (ya existía)
  - `ff46f79` fix(responsables): corregir reasignacion de responsable principal
  - `bd305ba` feat(responsables): completar gestion de vinculos desde alumnos
  - `403134f` docs: archivo `sonar-project.properties` (descubierto luego NO leído por el
    análisis automático de SonarQube Cloud — solo aplica a análisis CI/CD)
  - `8984d88` docs: handoff
  - `f53ef83` fix(tests): corregir nulabilidad de varargs SQL (`object?[]`) → elimina CS8625
  - `5e226be` chore(sonar): reemplazar por `.sonarcloud.properties` con
    `sonar.cpd.exclusions=database/baseline/` (config correcta del análisis automático)

## Estado inicial (Fase 0)
- Rama `feature/responsables-fase-018` sobre `main @ 35b95e0`; working tree limpio.
- Bloque 018A–018F implementado; PR #31 con Sonar en FAILURE (duplicación alta en
  New Code) y un build de CI flaky (testcontainers/ryuk).
- Frontend 018D solo visualizaba y desactivaba vínculos: faltaba vincular/editar/reactivar.

## Findings corregidos
1. **Bug de reasignación de principal (ACID).** `vincular_alumno_responsable`
   insertaba/reactivaba a B con `es_principal=true` ANTES de liberar al principal A:
   violaba el índice único parcial `ux_alumno_responsable_principal_activo` (23514).
   - Fix: reordenar para liberar el principal previo ANTES del INSERT/UPDATE, en ambos
     branches (reactivar e insert).
   - Concurrencia: `SELECT ... FOR UPDATE` sobre la fila del alumno en
     `vincular_alumno_responsable` y `editar_vinculo_responsable` (serializa las
     operaciones de vínculo del mismo alumno y preserva el invariante).
   - Mismo fix aplicado a la copia consolidada del baseline `001_schoolmanager_fase1a.sql`.
   - El índice único se conserva como defensa de DB.
2. **Duplicación Sonar (~34% new lines).** La causa real es el baseline consolidado
   `database/baseline/001_*.sql`, que reproduce todas las migraciones numeradas (mismo contenido que `017`, que reporta 94.9%) y se analizaba junto a la fuente
   autoritativa (las migraciones). Corrección aplicada:
   - Primer intento: `sonar-project.properties` con `sonar.exclusions=database/baseline/**`
     → **NO surtió efecto**: SonarQube Cloud documenta que el análisis automático
     (GitHub App) IGNORA `sonar-project.properties` (solo se usa en análisis CI/CD).
   - Corrección: se reemplazó por **`.sonarcloud.properties`** — el archivo que el
     análisis automático sí lee — con `sonar.cpd.exclusions=database/baseline/`
     (sin wildcards, conforme a la restricción del análisis automático). El baseline
     queda excluido SOLO de la detección de duplicación (CPD); el resto permanece analizado.
   - Fuente autoritativa para Sonar = `database/migrations/` (registro de cambios real);
     baseline = artefacto consolidado/generado → excluible sin ocultar deuda productiva.
   - Sin NOSONAR, sin bajar el Quality Gate, sin reformatear, sin excluir código productivo
     arbitrariamente.
3. **Warning CS8625** (`ResponsablesGestionTests.cs(275,97)`): un literal `null` se pasaba
   a los varargs SQL tipados como `object[]` (no anulable). Fix: declarar los varargs como
   `object?[]` (los parámetros SQL SÍ pueden ser null), manteniendo el guard `?? DBNull.Value`
   en `AddParameters`. Sin supresión/NOSONAR. El test afectado re-ejecutado → 11/11 sin warning.
4. Cierre previo (sesión anterior): 24 issues MAJOR limpiados (S2077 con NOSONAR en la
   línea exacta, S6964 en DTOs de request, accesibilidad web `id`/`for`).

## Archivos principales modificados
- `database/migrations/017_responsables_gestion_rpc.sql` — fix vincular/editar (order + FOR UPDATE).
- `database/baseline/001_schoolmanager_fase1a.sql` — copia consolidada con el mismo fix.
- `tests/SchoolManager.Database.IntegrationTests/Tests/ResponsablesGestionTests.cs` — test A→B + fix nulabilidad.
- `frontend/.../responsables.ts/.html/.css/.spec.ts` — flujo vincular completo + tests.
- `.sonarcloud.properties` — nuevo (reemplaza a `sonar-project.properties`, eliminado): exclusion CPD del baseline.

## Decisiones
- Corregir la migración 017 (no desplegada): válido por AGENTS.md §7 (migraciones manuales
  en Supabase tras CI). Sin evidencia de aplicación en DB persistente; solo validada en
  Postgres 16 desechable. NO se escribió remotamente.
- `FOR UPDATE` sobre la fila del alumno = solución más simple compatible con el diseño
  (sin advisory locks nuevos).
- Exclusión del baseline de Sonar = técnicamente correcta (artefacto consolidado/generado).
  Configuración correcta para el análisis automático = `.sonarcloud.properties`
  (no `sonar-project.properties`, que SonarQube Cloud ignora en modo automático).
  Documentado en `.sonarcloud.properties` y en este handoff.
- Frontend vincular reutiliza `listar()` (paginación/filtro server-side) para localizar
  candidatos sin descargar la tabla completa.

## Tests y resultados (Fase 6)
- DB: **91/91** (89 previos + 2 nuevos de reasignación A→B)
- API: **46/46**
- Frontend: **93/93** (87 previos + 6 nuevos del flujo vincular)
- Backend Release build: **OK** (0 warnings/errores)
- Frontend production build: **OK** (warnings de canvg/jspdf preexistentes, ajenos a 018)
- TSC: 0 errores · `git diff --check`: OK
- Secretos/artifacts: sin archivos no rastreados accidentales (`dist/` ignorado)

## Seguridad (Fase 7)
- Wrappers `rpc_*`: `security invoker`, `search_path=pg_catalog,public`.
- Internos: `security definer`, `search_path=pg_catalog,public,pg_temp` (seguro).
- Índice único de principal intacto; reactivación sin duplicados; motivo en desactivación.
- Npgsql nullables con `?? DBNull.Value` (patrón MatriculasController).

## Estado Sonar / CI / Vercel
> Test suites (sesión de cierre): DB **91/91**, API **46/46**, frontend **93/93**, builds OK,
> `git diff --check` OK, CS8625 eliminado (build de tests 0 warnings).
> CI (`Validar Compilación`) **pass** · Vercel/Preview **pass** · **SonarCloud: queued/pending**
> sobre el nuevo HEAD `3fe7baa` (push 2026-09-05 02:27Z; la check-run de sonarqubecloud no ha
> arrancado transcurridos ~11 min → infraestructura).
> `.sonarcloud.properties` con `sonar.cpd.exclusions=database/baseline/` vive en `main`
> (4ad58d1) y se conservó idéntica al integrar main en la rama.
> Última medida disponible del PR #31 (stale, pre-exclusión): `new_duplicated_lines_density`
> **34.12%** (1286 líneas; baseline 619 + 017 al 94.9% 608 + ResponsablesController 72, script
> vacío). Con la exclusión CPD del baseline se espera caer a ~2% (< 3% del gate Sonar way). El
> `project_status` de Sonar requiere auth (no disponible en sesión); el gate se confirma por
> check-run de sonarqubecloud.

## Pendientes reales
- **018G** auditoría read-only Supabase remota: sin credenciales de lector → documentado, no ejecutado.
- **Confirmar SonarCloud**: el análisis del nuevo HEAD `3fe7baa` (que usa la exclusion CPD que
  ya vive en `main` y fue rehecha en la rama por el merge) debe arrojar el baseline fuera de la
  duplicación y el gate verde. Al cierre de esta sesión la check-run de sonarqubecloud seguía
  queued/pending por infraestructura (no arrancada ~11 min tras el push) → re-verificar.
- No merge a main. No iniciar 019 (matriculas PERF ya documentado en 019).

## Advertencia Persona global
- Editar una Persona global compartida entre instituciones afecta a otras instituciones.
  No se rediseñó Persona; se mantuvo el diseño actual (Persona global + Responsable
  institucional). Validar en revisión antes del merge.

## Siguiente paso recomendado
1. Confirmar que el análisis del nuevo HEAD `3fe7baa` deja el Quality Gate verde en PR #31.
   La exclusion CPD ya vive en `main` (4ad58d1) y fue incorporada a la rama por el merge, así
   que el análisis automático (que lee el archivo del default branch) debe aplicarla.
2. Revisión humana focalizada: UX del panel de vínculos y reasignación de principal.
3. Coordinar 018G (auditoría Supabase remota) cuando haya credenciales de lector.
4. Merge a main cuando CI + Sonar + Vercel estén verdes.
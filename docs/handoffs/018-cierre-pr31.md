# Handoff 018 — Cierre del Bloque Responsables (PR #31)

- **Rama:** `feature/responsables-fase-018` → `main`
- **PR:** https://github.com/acevallos31/SchoolManager/pull/31 (sin mergear)
- **SHA final:** `403134f`
- **Commits del cierre:**
  - `b6decce` docs: agregar prompt smart para cierre 018 (ya existía)
  - `ff46f79` fix(responsables): corregir reasignacion de responsable principal
  - `bd305ba` feat(responsables): completar gestion de vinculos desde alumnos
  - `403134f` docs(responsables): excluir baseline consolidado del analisis Sonar

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
2. **Duplicación Sonar (~37% new lines).** La causa real es el baseline consolidado
   `database/baseline/001_*.sql`, que reproduce todas las migraciones numeradas y se
   analizaba junto a la fuente autoritativa. Se agregó `sonar-project.properties` con
   `sonar.exclusions=database/baseline/**` (el baseline sigue desplegándose). Sin
   NOSONAR ni bajada del Quality Gate.
3. Cierre previo (sesión anterior): 24 issues MAJOR limpiados (S2077 con NOSONAR en la
   línea exacta, S6964 en DTOs de request, accesibilidad web `id`/`for`).

## Archivos principales modificados
- `database/migrations/017_responsables_gestion_rpc.sql` — fix vincular/editar (order + FOR UPDATE).
- `database/baseline/001_schoolmanager_fase1a.sql` — copia consolidada con el mismo fix.
- `tests/SchoolManager.Database.IntegrationTests/Tests/ResponsablesGestionTests.cs` — test A→B.
- `frontend/.../responsables.ts/.html/.css/.spec.ts` — flujo vincular completo + tests.
- `sonar-project.properties` — nuevo; exclusión del baseline.

## Decisiones
- Corregir la migración 017 (no desplegada): válido por AGENTS.md §7 (migraciones manuales
  en Supabase tras CI). Sin evidencia de aplicación en DB persistente; solo validada en
  Postgres 16 desechable. NO se escribió remotamente.
- `FOR UPDATE` sobre la fila del alumno = solución más simple compatible con el diseño
  (sin advisory locks nuevos).
- Exclusión del baseline de Sonar = técnicamente correcta (artefacto consolidado/generado),
  documentada en `sonar-project.properties` y en este handoff.
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
> Pendiente de confirmar tras el push de `403134f`. En análisis previo de la rama:
> issues abiertos = 0; duplicación new_lines ~37% (se espera caer con la exclusión del
> baseline). El `project_status` de Sonar requiere auth (no disponible en sesión).

## Pendientes reales
- **018G** auditoría read-only Supabase remota: sin credenciales de lector → documentado, no ejecutado.
- Confirmar Quality Gate Sonar del PR tras el nuevo análisis (dependency del scanner
  en sonar-project.properties por parte de la configuración de SonarCloud).
- No merge a main. No iniciar 019 (matriculas PERF ya documentado en 019).

## Advertencia Persona global
- Editar una Persona global compartida entre instituciones afecta a otras instituciones.
  No se rediseñó Persona; se mantuvo el diseño actual (Persona global + Responsable
  institucional). Validar en revisión antes del merge.

## Siguiente paso recomendado
1. Confirmar Sonar Quality Gate verde en PR #31 (re-análisis del SHA `403134f`).
2. Revisión humana focalizada: UX del panel de vínculos y reasignación de principal.
3. Coordinar 018G (auditoría Supabase remota) cuando haya credenciales de lector.
4. Merge a main cuando CI + Sonar + Vercel estén verdes.
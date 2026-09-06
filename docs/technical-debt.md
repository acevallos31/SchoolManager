# Deuda técnica registrada — SchoolManager

Registro de deuda técnica **confirmada en el código**, sin resolver aún.
Cada entrada indica problema, riesgo, prioridad y cuándo abordarla.
Se prioriza cuando la deuda empieza a bloquear una fase o a hacer el
sistema frágil (principios ISW2 #4, #5 y #12).

> Regla: no registrar deuda especulativa. Toda entrada sale de una
> observación verificable en el repositorio.

---

## 1. Autorización frontend por permiso (guards) — sin cobertura central

- **Estado: PENDIENTE.**
- **Problema**: `app.routes.ts` no declara `canActivate` con guards. Existen
  `AdminGuard` y `PadreGuard` (rol `admin`, con comentario «compatibilidad de
  navegación») pero **no** están aplicados en el router; la verificación de
  permisos por operación vive dentro de componentes y, de forma autoritativa,
  en el backend. No hay guards que comprueben permisos concretos
  (`responsables.ver`, `conceptos_financieros.ver`, …) a nivel de ruta.
- **Riesgo**: navegación que muestra UI no accionable según el rol; la UI puede
  desincronizarse del permiso real. La seguridad **no** depende de esto (el
  backend autoriza), pero empeora la experiencia y puede filtrar opciones.
- **Prioridad**: Media.
- **Cuándo abordarlo**: cuando se consolide la navegación por rol/perfil
  (fase de UX/perfiles), junto con un modelo de permisos del frontend
  derivado del backend.

## 2. Grados y jornadas globales — riesgo futuro multiinstitución

- **Estado: PENDIENTE.**
- **Problema**: grados y jornadas son globales (sin `institucion_id`), mientras
  secciones son por institución/ciclo. La persona del alumno vive en una
  tabla única compartida entre instituciones. No hay selector global
  multiinstitución aún (hoy hay institución activa implícita).
- **Riesgo**: si se abre una segunda institución, datos de catálogo globales
  filtran entre instituciones y la persona compartida complica el aislamiento.
  RLS mitiga lo actual, pero el modelo no escala a multiinstitución limpia.
- **Prioridad**: Baja (hoy monoinstitucional) / Alta si se planifica multi.
- **Cuándo abordarlo**: antes de cualquier fase multiinstitución; como
  preparación de Fase 020 revisar si los catálogos de configuración financiera
  siguen el patrón por-institución.

## 5. Observabilidad de producción — mínima

- **Estado: PENDIENTE.**
- **Problema**: el único health check es `GET /health` (liveness básico del
  proceso: devuelve `200` con `{status, service, timestamp}`), pero **no**
  comprueba dependencias (Postgres/Auth): no es un healthcheck de readiness.
  No hay logging estructurado (serilog/OpenTelemetry) ni métricas de
  aplicación; solo el logging por consola de ASP.NET por defecto. No hay
  monitoreo del estado de los endpoints `/api` en producción. Ver
  `docs/observabilidad.md`.
- **Riesgo**: degradaciones o errores en producción pasan desapercibidos;
  diagnóstico lento. «Funciona en mi máquina» no es evidencia del servicio vivo.
- **Prioridad**: Media (post-020): lo que hoy protege es el CI + RLS, no el
  runtime.
- **Cuándo abordarlo**: una vez la fase 020 (cargos/mensualidades/cuentas por
  cobrar) toque producción con flujo de dinero, añadir mínimo healthcheck +
  logging de errores antes de nuevas superficies.

## 6. Coste de generación de mensualidades (backend de pagos/cuentas por cobrar)

- **Estado: PARCIAL.**
- **Problema**: la fase 020 materializó el modelo de **cargos** (obligaciones
  generadas) con su migración `019` y controlador `CargosController`/`/cargos`.
  **Pero** el backend de **pagos/cuentas por cobrar reales** (fase 021) sigue
  sin existir: no hay `MensualidadController` ni `PagoController`; la generación
  masiva de mensualidades con ACID (transacción única + verificación) está
  pendiente.
- **Riesgo**: implementar la generación sin considerar ACID (transacción
  única + verificación) repetiría errores de fases previas.
- **Prioridad**: Media (solo cuando se toque 021 con flujo de dinero).
- **Cuándo abordarlo**: durante el diseño de 021 (contrato + tests primero).

## 7. SonarCloud — cobertura generada en CI; import a Sonar pendiente de SONAR_TOKEN y paso manual

- **Estado: PARCIAL (infraestructura lista; import sigue pendiente de acción manual).**
- **Problema**: la cobertura **sí se genera** localmente y en CI (Coverlet →
  `coverage.cobertura.xml` para .NET; `@vitest/coverage-v8` → `lcov.info` para
  el frontend) y se sube como artifact en `deploy.yml`. El análisis automático
  de SonarCloud **no importa reportes de cobertura**: solo el modo **CI Analysis**
  (scanner con `SONAR_TOKEN` como secreto del repo) los consume.
- **Qué ya se ha hecho (cierre deuda post-020.5A, rama `chore/cierre-deuda-post-0205a`)**
  — infraestructura completa, sin depender del token:
  - `sonar-project.properties` en la raíz del monorepo: proyecto
    `SchoolManager`, organización `acevallos31`, cobertura backend Cobertura
    (`coverage-backend/**/coverage.cobertura.xml`) y frontend LCOV
    (`frontend/schoolmanager-frontend/coverage/**/lcov.info`), con exclusiones
    de `node_modules`/`dist`/`bin`/`obj`/`coverage`.
  - Job `sonarcloud` en `deploy.yml` (etapa 1.5): usa `sonar-scanner` CLI (no
    `dotnet-sonarscanner`, que no procesa LCOV de TypeScript), **condicionado a
    `if: ${{ secrets.SONAR_TOKEN != '' }}`** — sin el secret el job se salta y
    no importa nada, sin fallar ni bloquear el deploy.
- **Riesgo**: la métrica de cobertura en SonarCloud sigue vacía hasta que el
  mantenedor complete el paso manual; no se puede exigir Quality Gate de
  cobertura.
- **Prioridad**: Media.
- **Paso manual pendiente (SOLO el mantenedor, requiere el token)**:
  (a) añadir `SONAR_TOKEN` como secreto del repositorio; (b) **desactivar el
  análisis automático** en el panel de SonarCloud (Organization → Analysis
  Method → CI Analysis) — paso manual, no automatizable vía repo; (c) el job
  `sonarcloud` de `deploy.yml` se activará solo y usará `sonar-project.properties`.
  **No se debe afirmar que la cobertura ya se importa** mientras no ocurra.

## 8. Duplicación de código — estructural, no por permisos

- **Estado: PENDIENTE (informativa).**
- **Problema**: la duplicación reportada por SonarCloud (~22.5% histórica) no
  proviene de los strings de permisos (verificado: cada permiso
  `configuracion.*` / `academico.*` aparece definido una sola vez en el
  backend). Las fuentes reales son estructurales y conocidas: (a) el SQL de
  `validation/*.validation.sql` repite el esquema que valida contra su
  migración (p. ej. `018_configuracion_financiera.sql` 460 líneas vs su
  validation 159), y (b) modelos/páginas del frontend que reflejan DTOs del
  backend (p. ej. `mensualidades.ts` mantiene shapes `monto_pagado`,
  `monto_final` paralelos a `MensualidadDto.cs`).
- **Riesgo**: la métrica de duplicación de SonarCloud queda alta sin reflejar
  una duplicación de lógica de negocio real; puede llevar a refactors
  innecesarios si se interpreta mal. Parte de la duplicación SQL
  (`database/baseline/`) ya está excluida de CPD.
- **Prioridad**: Baja (informativa). No exige refactor grande hoy.
- **Cuándo abordarlo**: documentar la fuente de duplicación en el reporte de
  SonarCloud al configurar cobertura (#7); solo refactorizar si la
  duplicación frontend/DTO empieza a causar bugs de desincronización.

## 9. Cobertura de tests — línea base real y umbral propuesto

- **Estado: PENDIENTE (línea base documentada; sin umbral de CI activo).**
- **Línea base real (2026-09-05, reportes generados localmente con la misma
  configuración que el CI):**
  - **Backend** (`SchoolManager.API`, 13 archivos productivos, incl. `Program.cs`):
    **79.6%** de líneas (904/1136). El reporte Cobertura global del
    `dotnet test` mezcla también el ensamblado `SchoolManager.Database`
    (fixtures de tests de migración, no productivas), que al promediar lo
    reduce; por eso SonarCloud debe mirar solo el código productivo (ver
    exclusiones en `sonar-project.properties`).
  - **Frontend** (Vitest, 39 archivos): **Lines 63.98%** (1430/2235), Functions
    45.18%, Branches 50.37%.
- **Riesgo**: sin umbral no se evita regresión de cobertura; con un umbral
  arbitrario se bloquea por deuda histórica (sobre todo el frontend, que parte
  de 64%).
- **Prioridad**: Media.
- **Propuesta de umbral (diferenciando global vs New Code):**
  - **No imponer umbral global** de CI todavía: el backend va bien (88%) pero
    el frontend global (64%) refleja deuda histórica; un gate global bloquea
    todo PR por el frontend.
  - **Recomendado: Quality Gate de "New Code"** en SonarCloud una vez activo
    CI Analysis (#7), con línea base razonable: **Cobertura en New Code ≥ 80%
    para backend** y **≥ 70% para frontend** — así se exige cobertura en lo
    nuevo sin penalizar la deuda histórica acumulada.
  - Alternativa local/CI inmediata sin esperar a Sonar: umbral de regresión
    "no bajar de la línea base" (backend 79.6%, frontend 63.98%) en el runner,
    en vez de un % absoluto.
- **Cuándo abordarlo**: el gate de New Code con SonarCloud depende de completar
  #7 (paso manual del mantenedor). El umbral de regresión local puede
  implementarse en paralelo sin dependencias.

---

## Convenciones

- Nueva deuda descubierta → añadir aquí y, si procede, como issue con el
  template `tech-debt`.
- Una deuda resuelta → mover a la sección **Resuelta** o eliminar con
  referencia del PR/commit que la cerró.

---

## Resuelta

- **#3 Automatización de `validation/*.validation.sql`** — resuelta en 020.5A
  (`MigrationValidationSuiteTests` + `ValidationRunner` incremental sobre
  Postgres limpio; contrato «0 filas = pasa; SQL error o filas = falla»).
  Tests 2/2 PASS en la suite DB.
- **#4 Verificación/checksum de `schema_migrations`** — resuelta en 020.5A
  (`MigrationRunner` verifica SHA-256, backfill de NULL, fail en divergencia
  con `MigrationChecksumMismatchException`). `schema_migrations` ya tenía la
  columna `checksum text null` desde la 001; 10/10 PASS en la suite DB.

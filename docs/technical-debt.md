# Deuda técnica registrada — SchoolManager

Registro de deuda técnica **confirmada en el código**, sin resolver aún.
Cada entrada indica problema, riesgo, prioridad y cuándo abordarla.
Se prioriza cuando la deuda empieza a bloquear una fase o a hacer el
sistema frágil (principios ISW2 #4, #5 y #12).

> Regla: no registrar deuda especulativa. Toda entrada sale de una
> observación verificable en el repositorio.

---

## 1. Autorización frontend por permiso (guards) — sin cobertura central

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

## 6. Coste de generación de mensualidades (si la fase 020 lo introduce)

- **Problema**: no existe aún backend de mensualidades/cuentas por cobrar.
  El `modelo` `Mensualidad`/`Pago` existe solo en frontend y como entidades
  sin controlador. Se documenta aquí para no introducir el problema al
  implementar la fase 020 (ver planificación 020 en el reporte, no en este
  PR).
- **Riesgo**: implementar la generación sin considerar ACID (transacción
  única + verificación) repetiría errores de fases previas.
- **Prioridad**: N/A hasta 020.
- **Cuándo abordarlo**: durante el diseño de 020 (contrato + tests primero).

## 7. SonarCloud — cobertura generada en CI pero import a Sonar pendiente de modo CI Analysis

- **Problema**: la cobertura **sí se genera** localmente y en CI (Coverlet →
  `coverage.cobertura.xml` para .NET; `@vitest/coverage-v8` → `lcov.info` para
  el frontend) y se sube como artifact en `deploy.yml`. **Pero** SonarCloud se
  integra por análisis **automático** (GitHub App, solo `sonar.cpd.exclusions`
  en `.sonarcloud.properties`), y el análisis automático **no importa reportes
  de cobertura**: solo el modo **CI Analysis** (scanner con `SONAR_TOKEN` como
  secreto del repo) los consume. No hay `sonar-project.properties` ni job de
  scanner.
- **Riesgo**: la métrica de cobertura en SonarCloud sigue vacía hasta migrar a
  CI Analysis; no se puede exigir Quality Gate de cobertura.
- **Prioridad**: Media.
- **Cuándo/requisitos**: PR propio (fuera de este) que necesitará, como paso
  manual del mantenedor: (a) añadir `SONAR_TOKEN` como secreto del repo y job
  `sonar-scanner` en CI; (b) **desactivar el análisis automático** en el panel
  de SonarCloud (Organization → Analysis Method) para evitar doble análisis —
  paso manual, no automatizable vía repo; (c) crear `sonar-project.properties`
  apuntando a los reportes ya generados:
  `sonar.cs.cobertura.reportsPaths=coverage-backend/**/coverage.cobertura.xml`
  y `sonar.typescript.lcov.reportPaths=frontend/schoolmanager-frontend/coverage/**/lcov.info`.
  No establecer umbral de cobertura hasta tener línea base real.

## 8. Duplicación de código — estructural, no por permisos

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

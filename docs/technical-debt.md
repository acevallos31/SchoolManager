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

- **Problema**: el único health check era `GET /health` (liveness básico del
  proceso: devuelve `200` con `{status, service, timestamp}`), pero **no**
  comprobaba dependencias (Postgres/Auth): no era un healthcheck de
  readiness. No había logging estructurado (serilog/OpenTelemetry) ni
  métricas de aplicación; solo el logging por consola de ASP.NET por defecto.
  No hay monitoreo del estado de los endpoints `/api` en producción. Ver
  `docs/observabilidad.md`.
- **Estado (PR A #5)**: el readiness quedó implementado en `GET /health/ready`
  — comprueba conectividad real con PostgreSQL vía `NpgsqlDataSource`
  (`SELECT 1`, timeout 3 s): `200` cuando responde, `503` cuando la base falla;
  no expone secretos ni connection strings. `GET /health` se mantiene como
  liveness. Pendiente de merge del PR A.
- **Riesgo**: degradaciones o errores en producción pasan desapercibidos;
  diagnóstico lento. «Funciona en mi máquina» no es evidencia del servicio vivo.
- **Prioridad**: Media (post-020): lo que hoy protege es el CI + RLS, no el
  runtime.
- **Cuándo abordarlo**: (resuelto por el readiness en PR A) el logging
  estructurado y las métricas de aplicación quedan como deuda futura separada
  si se necesita trazabilidad en producción.

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

## 8. Duplicación de código — estructural (no por permisos)

- **Estado: PARCIAL — lo corregible corregido en PR A #41; lo deliberado
  documentado como aceptado; portal-padre pendiente junto al bloque 021.**
- **Problema**: la duplicación reportada por SonarCloud (~22.5% histórica) no
  proviene de los strings de permisos (verificado: cada permiso
  `configuracion.*` / `academico.*` aparece definido una sola vez en el
  backend). Las fuentes reales son estructurales.
- **Resuelta en PR A (deuda #8, 2026-09-05)** — lo corregible se corrigió:
  1. *Boilerplate de controllers duplicado* (5 controllers repetían
     `AbrirComoUsuarioAsync` + `FijarClaimAsync` + `ToError` con un switch
     divergente: Cargos/Conceptos/Planes no contemplaban `SM001`/`SM003`
     explícitamente). Extraído a base `Controllers/ApiControllerBase.cs`
     (switch unificado → `SM001`/`SM003` → 400). Sin cambio de comportamiento
     (ya caían en el default 400).
  2. *Código muerto*: `DTOs/MensualidadDto.cs` (MensualidadDto,
     MensualidadResponseDto, MensualidadCreateDto, PagoCreateDto,
     DescuentoDto) y `Models/Mensualidad.cs`, `Models/Pago.cs` — 0 referencias
     de controller/test (verificado por grep exhaustivo). Eliminados.
- **Aceptada/deliberada (documentada, sin refactor)**:
  - SQL de `validation/*.validation.sql` que repite el esquema de su migración:
    deliberado (valida contra el esquema real) y ya excluido de CPD
    (`database/baseline/`).
  - Duplicación frontend-refleja-DTO (`MatriculaDto.cs` ↔
    `matriculas.service.ts`, etc.): barrera estructural TS/C#, alineada hoy
    (nombres y nullabilidad idénticos). No refactorizar.
  - Estados de matrícula como strings (`'pendiente'`, `'activa'`, …)
    SQL↔backend↔frontend: sin divergencia verificada hoy. Sin refactor.
- **Nuevo hallazgo (pendiente, no es deuda #8 corregible aquí)**:
  `frontend/.../pages/portal-padre/portal-padre.ts` (enrutado desde login)
  consume vía Supabase un esquema inexistente: `alumnos.tutor_id` (no existe
  en DDL), y tablas `mensualidades`/`pagos` con columnas `monto_final`,
  `fecha_pago` (no hay `CREATE TABLE` ni RPC de ellas; la migración 019 solo
  crea `cargos`). Es funcionalidad de pagos del **bloque 021** (fuera de
  alcance actual). Re-cablear la página a cargos reales no es una abstracción
  simple: se documenta y se resuelve junto a 021.
- **Riesgo**: la métrica de duplicación de SonarCloud puede seguir alta en el
  código estructural restante, sin reflejar duplicación de lógica de negocio.
- **Prioridad**: Baja (informativa) tras la corrección anterior.

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
  - **No imponer umbral global** de CI todavía: el backend va bien (79.6%) pero
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

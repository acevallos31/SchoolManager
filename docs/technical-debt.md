# Deuda técnica registrada — SchoolManager

Registro de deuda técnica **confirmada en el código**, sin resolver aún.
Cada entrada indica problema, riesgo, prioridad y cuándo abordarla.
Se prioriza cuando la deuda empieza a bloquear una fase o a hacer el
sistema frágil (principios ISW2 #4, #5 y #12).

> Regla: no registrar deuda especulativa. Toda entrada sale de una
> observación verificable en el repositorio.

---

## 1. Autorización frontend por permiso (guards) — sin cobertura central

- **Estado: RESUELTO (PR de hardening pre-021)** — alternativa mínima de la
  opción (a), sin rediseño del router.
- **Problema (root cause)**: `AuthService` cargaba la sesión de forma
  **asíncrona y fire-and-forget** (`getSession().then(...)` en el constructor)
  y `AdminGuard`/`PadreGuard` leían estado **síncrono** de `BehaviorSubject`s
  sin poblar; además `app.routes.ts` **no aplicaba ningún guard**. No existía
  un modelo de permisos cargado y esperado **antes** de resolver las rutas, por
  lo que un guard evaluaría contra estado sin cargar (carrera).
- **Solución (barrera de inicialización + guard reutilizable)**:
  - `provideAppInitializer(() => inject(AuthService).asegurarUsuarioInicial())`
    en `app.config.ts`: espera la restauración de sesión y `/auth/me` **antes**
    del bootstrap. `asegurarUsuarioInicial()` es **idempotente** (una sola
    ejecución), nunca lanza (error de sesión o de `/auth/me` → estado "sin
    sesión", no bloquea el bootstrap) y **reutiliza** la lógica existente de
    `restaurarSesion`/`getUsuarioActual` (refactorizada, no duplicada).
  - Nuevo `PermissionGuard` funcional (`core/guards/permission.guard.ts`) que
    lee `route.data['permiso']` contra el modelo de permisos de `AuthService`
    (respaldado por `/auth/me`). Sin sesión → `/login`; sesión sin permiso →
    `/dashboard` (no existe ruta 403; limitación documentada, no se crea
    mini-módulo); con permiso → acceso; sin `data.permiso` → guard de
    autenticación puro. No sustituye la autorización backend/RLS (solo
    navegación/UI coherente).
  - Rutas cableadas **solo con permiso verificable** en `Permisos.cs`:
    `academico.alumnos.ver`, `academico.matriculas.ver`,
    `academico.responsables.ver`, `academico.cargos.ver`,
    `configuracion.conceptos_financieros.ver`, `configuracion.planes_pago.ver`.
    Rutas de autenticación pura sin permiso concreto: `dashboard`,
    `configuracion`, `configuracion/ciclos`, `configuracion/estructura-academica`.
  - `AdminGuard`/`PadreGuard` quedaron sin uso (ninguna ruta los usaba) y se
    **eliminaron en 023** (código muerto). `portal-padre` y `login` no requieren
    guard adicional: la autorización la valida el backend.
- **Pruebas**: 7 casos nuevos en `auth.spec.ts` (restauración de sesión, sin
  sesión, error de `/auth/me` no bloquea, idempotencia) y 8 en
  `permission.guard.spec.ts` (permiso→acceso, sin permiso→/dashboard,
  no autenticado→/login, permisos distintos por módulo, ruta sin permiso,
  contexto multiinstitución no se modifica, el guard no sustituye al backend).
- **Riesgo residual**: las rutas `configuracion/ciclos`,
  `configuracion/estructura-academica` y `configuracion` (raíz) y `dashboard`
  se protegen **solo por autenticación** porque **no existe un permiso backend
  inequívoco** en `Permisos.cs` para ellas (no hay `configuracion.ciclos.ver`
  ni `estructura_academica.ver`; el app-shell usa `configuracion.sistema.ver` /
  `configuracion.instituciones.ver` que **no** están definidos en el backend).
  Aplicarles un permiso exigiría un permiso backend nuevo (fuera de alcance).
- **Prioridad**: Media.
- **Cuándo abordarlo**: no aplica (resuelto en su alternativa mínima). Si se
  quieren proteger las rutas genéricas de configuración por permiso, hay que
  definir esos permisos en el backend primero.

## 2. Grados y jornadas globales — riesgo futuro multiinstitución

- **Estado: RESUELTO (PR B, deuda #2)** — migración `020_grados_jornadas_multiinstitucion`.
- **Problema (root cause)**: grados y jornadas eran catálogos globales (sin
  `institucion_id`) mientras secciones/ciclos/matrículas ya eran por institución.
  Las RPC de configuración (`rpc_listar/crear/actualizar/cambiar_estado_*`) sólo
  validaban el permiso del rol pero **no filtraban sus SELECT por institución**, y
  el RLS de `grados`/`jornadas` usaba `usuario_tiene_permiso_en_algun_ambito` (una
  institución autorizada en cualquier ámbito podía leer el catálogo de todas).
  Resultado: una institución autorizada veía/editaria los catálogos de todas las
  demás — fuga de aislamiento multiinstitución.
- **Decisión de modelo**: `institucion_id` **directo** en `grados` y `jornadas`
  (el patrón canónico por-institución de `secciones`/`ciclos_escolares`/
  `conceptos`/`planes`/`responsables`); sin tabla puente intermedia.
- **Backfill**: determinista. Modo monoinstitucional → todo se asigna a la
  institución activa única. Modo multiinstitucional → se infiere por referencia
  inequívoca desde `secciones`; un grado/jornada compartido por varias
  instituciones u huérfano **aborta la migración con error explícito** (no se
  duplica silenciosamente, preservando identidad funcional). `NOT NULL` sólo
  después del backfill.
- **Invariantes tras 020**:
  - FK a `institucion_id` → `instituciones(id)` en grados y jornadas.
  - Unicidad de nombre **por institución** (`ux_*_institucion_nombre` sobre
    `(institucion_id, lower(btrim(nombre)))`); se retira el `UNIQUE(nombre)`
    global pre-020 (`grados_nombre_key`/`jornadas_nombre_key`).
  - FK **compuesta** en `secciones`: `(grado_id, institucion_id)` y
    `(jornada_id, institucion_id)` — una sección sólo puede referenciar
    grado/jornada de su misma institución.
  - RLS de lectura scoped por fila (`usuario_tiene_permiso_actual(…, institucion_id)`).
  - RPC filtran sus `SELECT`/`UPDATE` por `institucion_id` (contexto resuelto vía
    `resolver_institucion_operacion`) y devuelven la columna `institucion_id`.
  - Grants a `authenticated` cubren todas las firmas (incl. `rpc_cambiar_estado_*`).
- **Pruebas**: 16 casos multitenancy en `GradosJornadasMultitenancyTests`
  (aislamiento de listado, no-lectura/no-escritura/no-cambio-de-estado por UUID
  ajeno, mismos nombres en instituciones distintas, duplicado intra-institución
  rechazado, FK compuesta de secciones, contexto multi-rol, denegación explícita).
  Más validations SQL de 020 (cero filas en diagnósticos `*_faltante`/`*_indebido`)
  y round-trip rollback→reapply en `RollbackTests`.
- **Legado que conserva catálogo global** (a propósito): seed de la baseline
  `001` (resuelto por el backfill al migrar), migraciones históricas `008`/`016`,
  y `rollback/020` (restaura el modelo global).
- **Riesgo/pendiente**: nada conocido tras 020; el Bloque 021 (pagos, `portal-padre`,
  `Vertic`) queda fuera de alcance y se consume el esquema de pagos existente.
- **Prioridad**: Resuelta por esta deuda.
- **Cuándo/archivos**: `database/migrations/020_*.sql`, `rollback/020_*.sql`,
  `validation/020_*.sql`, `tests/…/GradosJornadasMultitenancyTests.cs`,
  `tests/…/{AcademicModel,SchemaConstraints,CargosMultitenancy,RlsSecurity,
  ResponsablesGestion,AcademicStructureConfiguration}Tests.cs`,
  factories `CargosApiFactory`/`MatriculasApiFactory`, `perf/benchmark/seed.sql`.
  Handoff: `docs/handoffs/020-grados-jornadas-multiinstitucion.md`.

## 5. Observabilidad de producción — mínima

- **Problema**: el único health check era `GET /health` (liveness básico del
  proceso: devuelve `200` con `{status, service, timestamp}`), pero **no**
  comprobaba dependencias (Postgres/Auth): no era un healthcheck de
  readiness. No había logging estructurado (serilog/OpenTelemetry) ni
  métricas de aplicación; solo el logging por consola de ASP.NET por defecto.
  No hay monitoreo del estado de los endpoints `/api` en producción. Ver
  `docs/observabilidad.md`.
- **Estado (PR A #5, MERGEADO en main)**: el readiness quedó implementado en
  `GET /health/ready` — comprueba conectividad real con PostgreSQL vía
  `NpgsqlDataSource` (`SELECT 1`, timeout 3 s): `200` cuando responde, `503`
  cuando la base falla; no expone secretos ni connection strings. `GET /health`
  se mantiene como liveness. Verificado de nuevo en el PR de hardening pre-021
  (`/health` liveness y `/health/ready` chequea Postgres en `Program.cs`;
  `HealthReadinessTests.cs` en verde; **sin** logging/telemetría añadidos en
  este PR).
- **Riesgo**: degradaciones o errores en producción pasan desapercibidos;
  diagnóstico lento. «Funciona en mi máquina» no es evidencia del servicio vivo.
- **Prioridad**: Media (post-020): lo que hoy protege es el CI + RLS, no el
  runtime.
- **Cuándo abordarlo**: (resuelto por el readiness mergeado) el logging
  estructurado y las métricas de aplicación quedan como deuda futura separada
  si se necesita trazabilidad en producción.

## 6. Coste de generación de mensualidades (backend de pagos/cuentas por cobrar)

- **Estado: RESUELTO (Bloque 021, en main — PR #44 `f61c931`).**
- **Problema (histórico)**: la fase 020 materializó el modelo de **cargos**
  (obligaciones generadas) con su migración `019` y controlador `CargosController`,
  pero el backend de **pagos/cuentas por cobrar** (fase 021) aún no existía
  (sin `PagoController`, sin modelo transaccional).
- **Resuelto en 021**: migración `021_pagos_cobranza` (`pagos` + `pagos_aplicaciones`,
  saldo siempre derivado, triggers de sincronización de `cargos.estado`, anulación
  atómica sin DELETE físico) y `PagosController` (`/api/pagos`) con registro/anulación
  ACID. Detalle: `docs/handoffs/021-pagos-cobranza.md`.
- **Prioridad**: Resuelta.

## 7. SonarCloud — cobertura generada en CI; import a Sonar pendiente de SONAR_TOKEN y paso manual

- **Estado: RESUELTO (Bloque 029, 2026-09-07).** Análisis real de SonarCloud
  en verde; cobertura backend (Cobertura) y frontend (LCOV) importadas; Quality
  Gate del PR #52 en verde. Run validado: **34154637093** (head `e76cedc`).
- **Problema (histórico)**: la cobertura **sí se genera** localmente y en CI
  (Coverlet → `coverage.cobertura.xml` para .NET; `@vitest/coverage-v8` →
  `lcov.info` para el frontend) y se sube como artifact en `deploy.yml`. El
  análisis automático de SonarCloud **no importa reportes de cobertura**: solo
  el modo **CI Analysis** (scanner con `SONAR_TOKEN` como secreto del repo) los
  consume.
- **Qué se hizo en el Bloque 029 (cierre)**:
  - `SONAR_TOKEN` configurado como secret del repo y **válido**
    (validado contra `/api/authentication/validate` → `valid=true`).
  - Job `sonarcloud` migrado de la descarga manual del CLI
    (`sonar-scanner-cli-7.1.0.12063`, moría con `exit 8` sin salida útil) a la
    **action oficial `SonarSource/sonarqube-scan-action@v8.2.1`** (auto-
    aprovisiona JRE y scanner; se eliminó `setup-java` del job).
  - **Causa raíz corregida**: `sonar.sources`/`sonar.tests` usaban wildcards
    (`**`, `*`), rechazadas por SonarScanner 8.x (exit 3). Pasaron a **solo
    directorios** (`backend,frontend/schoolmanager-frontend/src` y `tests`).
    Commits `f635a23` (action) y `e76cedc` (directorios).
  - Guard anti falso-verde conservado: si falta `SONAR_TOKEN`, el job **falla
    en rojo** en vez de quedar verde sin analizar.
- **Riesgo residual**: desactivar *Automatic Analysis* en el panel de
  SonarCloud es un paso manual del mantenedor (evita análisis duplicados); el
  pin de la action `v8.2.1` conviene revisarlo al publicarse versiones nuevas.
- **Prioridad**: Resuelta.

## 8. Duplicación de código — estructural (no por permisos)

- **Estado: RESUELTO (PR A #41 en main; sub-entrada de portal-padre resuelta por 022).**
- **Problema**: la duplicación reportada por SonarCloud (~22.5% histórica) no
  proviene de los strings de permisos (verificado: cada permiso
  `configuracion.*` / `academico.*` aparece definido una sola vez en el
  backend). Las fuentes reales son estructurales.
- **Resuelta en PR A (deuda #8, en main — `95e834b`)** — lo corregible se corrigió:
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
- **Resuelta en 022 (PR #45, en main — `bed6a85`)**: la página
  `frontend/.../pages/portal-padre/portal-padre.ts` se reescribió para consumir
  la **API .NET** (`PortalResponsableController`) en modo lectura real contra
  cargos/pagos/RLS existentes (021), eliminando el acceso vía Supabase al
  esquema inexistente (`alumnos.tutor_id`, tablas `mensualidades`/`pagos` legacy
  que la migración 019 nunca creó). La deuda de portal-padre queda **resuelta**;
  re-cablear no requirió tocar 021. Detalle: `docs/handoffs/022-portal-responsable.md`.
- **Riesgo**: la métrica de duplicación de SonarCloud puede seguir alta en el
  código estructural restante, sin reflejar duplicación de lógica de negocio.
- **Prioridad**: Baja (informativa) tras la corrección anterior.

## 9. Cobertura de tests — línea base real y umbral propuesto

- **Estado: PARCIAL→ACTUALIZADA (Bloque 029, 2026-09-07).** Gate de regresión
  local/CI activo; el New Code real de SonarCloud **ya está disponible** con la
  deuda #7 resuelta (análisis CI Analysis en verde). Pendiente solo decidir/aplicar
  el umbral de New Code (ver abajo).
- **Gate implementado (`scripts/check-coverage-gate.py` + step en
  `deploy.yml`/`validate-code`)**: compara la cobertura de líneas generada por
  CI contra un **baseline versionado** (`docs/coverage-baseline.json`) y falla
  solo si la actual cae por debajo de `baseline - 1.0 punto` (tolerancia para
  absorber fluctuaciones de medición, no regresiones reales). **No** impone un
  umbral global aspiracional sobre el histórico, por lo que no bloquea PRs por
  la deuda histórica del frontend (64%) ni exige subir cobertura.
- **Baselines medidos (2026-09-06, misma config que el CI):**
  - **Backend** (`SchoolManager.API`, paquete productivo, excluye tests):
    **81.54%** líneas (1758/2156) — subió vs el 79.6% histórico.
  - **Frontend** (lcov.info, 40 archivos): **64.86%** líneas (1460/2251).
- **Nuevo con 029**: con el CI Analysis real en verde (deuda #7 resuelta), la
  métrica de **New Code / diff coverage** de SonarCloud ya se calcula para el
  PR #52 y el Quality Gate del PR **pasa en verde**. Queda **recomendado**
  configurar el Quality Gate de New Code ≥ 80% backend / ≥ 70% frontend
  (según la propuesta original) en el panel de SonarCloud — paso manual del
  mantenedor, no automatizable vía repo.
- **Limitación documentada**: el gate local protege contra **regresiones
  globales**, no contra caídas de cobertura en código **nuevo** (eso lo cubre
  el New Code de SonarCloud, ahora ya medible). El gate local es un mínimo de
  contención, no un sustituto del New Code.
- **Riesgo**: si se añade mucho código nuevo sin tests, la cobertura global
  puede no caer bajo el gate pese a bajar la cobertura marginal de lo nuevo,
  a menos que se aplique el umbral de New Code en SonarCloud.
- **Prioridad**: Media.
- **Cuándo abordarlo**: la parte restante (aplicar el umbral de New Code en
  SonarCloud) es un paso manual del mantenedor en el panel; el gate de regresión
  local ya está activo y no requiere infraestructura externa.

## 10. Páginas de negocio que consultan Supabase directo (sin pasar por la API .NET)

- **Estado: ABIERTO (deliberado) — no es un flujo roto; funciona contra Supabase.**
- **Problema**: varias páginas de negocio leen/escriben **directo contra
  Supabase** (vía `SUPABASE_CLIENT`) en lugar de la API .NET, rompiendo el
  patrón del resto del frontend. Afecta a:
  - `pages/alumnos` y `pages/matriculas` → `core/services/alumno.service.ts`
    (`.from('alumnos')`, RPC `rpc_*`), pese a que existe (o existía) un
    controlador .NET equivalente. El `AlumnosController` era un **stub** que
    nunca consultó Postgres y se eliminó en 023.
  - `pages/configuracion/ciclos` → `core/services/ciclo-escolar.service.ts`
    (RPC `rpc_listar/crear_*_ciclo_escolar`, `rpc_*_periodo_matricula`).
  - `pages/configuracion/estructura-academica` → `estructura-academica.service.ts`
    y `configuracion.service.ts` (RPC de grados/jornadas/secciones).
- **Por qué no se resuelve en 023**: los flujos **funcionan** contra Supabase
  real y, para ciclos/estructura-académica/configuración, **no existe un
  controller .NET** que reemplace el acceso (migrarlos = construir endpoints
  nuevos + re-cablear páginas, es decir, **funcionalidad nueva**, no reparación).
  La única vía coherente es un **bloque dedicado de migración a API .NET** con
  tests por cada dominio.
- **Riesgo**: dependencia de Supabase directo en el frontend (menos centralizado
  que el patrón API .NET); fuga de la regla «backend .NET → Postgres».
- **Prioridad**: Media (bloque de arquitectura posterior a 023/UX).
- **Cuándo abordarlo**: bloque dedicado de migración a la API .NET, tras el
  cierre funcional 023 y el bloque visual/UX.

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

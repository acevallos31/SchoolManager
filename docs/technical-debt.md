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

- **Estado: RESUELTO (Bloque 029 + 029B, 2026-09-07, PR #52 `6e3299a`).** Análisis
  estático **completo** en CI: frontend TS + **backend C# real** + coberturas
  (C# Cobertura + LCOV frontend), con Quality Gate **verde y representativo**.
  El residual del 029 (C# no analizado por el scanner genérico) quedó **cerrado**
  en el 029B. Confirmado en logs (run `34160443496`): el warning
  `C# files which cannot be analyzed with the scanner you are using` **ya no
  aparece** (0 ocurrencias) y Sonar **procesa realmente los `.cs`** (módulos
  `SchoolManager.API`, `SchoolManager.API.IntegrationTests`,
  `SchoolManager.Database.IntegrationTests` indexados).
- **Problema (histórico)**: la cobertura **sí se genera** localmente y en CI
  (Coverlet → `coverage.cobertura.xml` para .NET; `@vitest/coverage-v8` →
  `lcov.info` para el frontend) y se sube como artifact en `deploy.yml`. El
  análisis automático de SonarCloud **no importa reportes de cobertura**: solo
  el modo **CI Analysis** (scanner con `SONAR_TOKEN` como secreto del repo) los
  consume.
- **Qué se hizo en el Bloque 029 (avance previo)**:
  - `SONAR_TOKEN` configurado como secret del repo y **válido**.
  - Migración de la descarga manual del CLI (exit 8) a la action oficial
    `SonarSource/sonarqube-scan-action@v8.2.1` (commits `f635a23`/`e76cedc`),
    con `sonar.sources`/`sonar.tests` en **solo directorios** (sin wildcards).
  - Guard anti falso-verde: si falta `SONAR_TOKEN`, el job **falla en rojo**.
- **Qué se hizo en el Bloque 029B (residual C# cerrado, commits `11b5146` →
  `6e3299a`)**: se sustituyó la action genérica por **SonarScanner for .NET**
  (`dotnet-sonarscanner`, tool v11.3.0) con flujo `begin → dotnet build (tests)
  → end` en el job `sonarcloud` de `deploy.yml`:
  - Se **eliminó `sonar-project.properties`** (el scanner .NET NO lo lee y da
    error si existe); todas las propiedades se pasan como `/d:` en el `begin`
    (`sonar.organization=acevallos31`, `sonar.projectKey=SchoolManager`,
    `sonar.host.url=https://sonarcloud.io`, `sonar.scanner.scanAll=true` para
    conservar el análisis TS/frontend, `sonar.cs.cobertura.reportsPaths` para la
    cobertura backend Cobertura, `sonar.typescript.lcov.reportPaths` para el
    frontend, y `sonar.exclusions` que ahora **incluye `e2e/**`**). Se fijó
    `set -f` en el paso para que los patrones `**` pasen literales al scanner.
    `sonar.sources`/`sonar.tests` NO se pasan: el scanner .NET los ignora con un
    WARNING (no los soporta) — C#/tests se obtienen de los `.csproj` que se
    compilan y `scanAll=true` incorpora el TS; el alcance se afina solo con
    exclusions/inclusions.
  - Se añadió el paso **`SonarSource/sonarqube-quality-gate-action`** (pineado
    por SHA `7a5fffe8e523c40e0c740b6bc2712ab503e52efa` = v1.2.1) tras el `end`:
    lee `report-task.txt`, consulta el QG del PR y **falla el job si no es
    verde** — anti falso-verde extendido al Quality Gate (la app de SonarCloud
    no crea check QG con el scanner CLI).
  - **El QG quedó genuinamente representativo**: al analizar C# por primera vez
    afloraron 2 vulnerabilidades en código nuevo del propio `deploy.yml`
    (`githubactions:S8482` BLOCKER por un paso diagnóstico `curl | python3`, y
    `githubactions:S7637` MAJOR por la action QG en tag en vez de SHA), que
    eran **invisibles con el scanner genérico**. Se corrigieron (paso
    diagnóstico eliminado; action pineada por SHA) y el QG pasó verde. Esto es
    la prueba de que el anti falso-verde funciona: el rojo previo era legítimo,
    no un artefacto.
- **Qué SÍ se analiza e importa hoy (verificado en el log del run `34160443496`):**
  - Backend C#: análisis estático real (módulos .NET indexados, sin skip).
  - Frontend TypeScript/Angular: análisis real (`Creating TypeScript(6.0.3)
    program ...tsconfig.json`, `Analyzing 59 file(s)`).
  - Coberturas: backend Cobertura (`Parsing the Cobertura report
    ...coverage.cobertura.xml`; `Coverage Report Statistics: 39 files, 15 main
    files, 15 main files with coverage`) y frontend LCOV (`Analysing ...lcov.info`).
  - Quality Gate: **`✔ Quality Gate has PASSED`** (`ANALYSIS SUCCESSFUL`,
    dashboard `pullRequest=52`).
- **Riesgo residual (menor, no bloquea)**: otros `uses:` del workflow siguen en
  tags (`@v4`) y no en SHA — no fueron señalados como vulnerabilidades de código
  nuevo (no están en el diff del PR), pero conviene pinearlos por SHA en un
  futuro hardening. El pin `@v8.2.1` de `sonarqube-scan-action` quedó obsoleto
  al migrar a SonarScanner for .NET; revisar al publicarse versiones nuevas.
- **Prioridad**: Resuelta (alta hasta el 029B).

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

- **Estado: ACTUALIZADA / RESUELTA (Bloque 029 + 029B, 2026-09-07).** Con el
  Bloque 029B, el **Quality Gate de SonarCloud ya es representativo del backend**:
  el análisis estático C# real corre (deuda #7 resuelta) y el QG del PR #52 pasa
  **verde** sobre New Code que incluye backend + frontend + coberturas. La
  métrica de New Code / diff coverage de SonarCloud ahora se calcula sobre todo
  el proyecto (no solo frontend).
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
- **Nuevo con 029B**: el QG del PR #52 pasa verde con análisis C# real (run
  `34160443496`, `✔ Quality Gate has PASSED`). Queda **recomendado** configurar
  el Quality Gate de New Code ≥ 80% backend / ≥ 70% frontend en el panel de
  SonarCloud (paso manual del mantenedor), ahora que el backend C# está
  analizado y la métrica es real.
- **Limitación documentada**: el gate local protege contra **regresiones
  globales**, no contra caídas de cobertura en código **nuevo** (eso lo cubre
  el New Code de SonarCloud, hoy ya calculado sobre backend + frontend). El
  gate local es un mínimo de contención, no un sustituto del New Code.
- **Riesgo**: si se añade mucho código nuevo sin tests, la cobertura global
  puede no caer bajo el gate pese a bajar la cobertura marginal de lo nuevo,
  a menos que se aplique el umbral de New Code en SonarCloud.
- **Prioridad**: Media.

## 10. Páginas de negocio que consultan Supabase directo (sin pasar por la API .NET)

> **Actualización 030F — Codex, 2026-09-08: RESUELTA en PR #53 (sin merge).**
> Esta nota sustituye el estado abierto descrito abajo, que se conserva como
> diagnóstico histórico. 030B–030F migraron los servicios de negocio a .NET.
> Búsqueda global: 0 accesos directos de negocio; única excepción productiva
> `auth.ts` (Supabase Auth). Sus mocks y `Array.from` nativo no son accesos de negocio.
> API 156/156, DB 156/156, frontend 289/289 y builds locales correctos.
> CI y Vercel verdes en `3feb517`, run `34192575139`. SonarScanner for .NET:
> C# real + TypeScript, Cobertura/LCOV importados, Quality Gate **OK**;
> New Code 86,5% cobertura y 2,7% duplicación, sin debilitar reglas ni umbrales.
> Contratos, clasificación de las cinco RPC de Configuración y riesgos:
> `docs/handoffs/030F-cierre-global.md`. PR #53 abierto, sin merge.

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

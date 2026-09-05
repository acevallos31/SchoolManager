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

## 3. Automatización de `validation/*.validation.sql` — pendiente en pipeline

- **Problema**: cada migración trae su `validation/` SQL, pero no se ejecuta de
  forma automatizada en CI ni en la suite. Hoy solo se validan explícitamente
  los casos que tienen test dedicado (p. ej. los de responsabilidad de
  integración); el resto depende de revisión manual.
- **Riesgo**: una migración puede cumplir el versionado pero romper una
  invariante que su propio `validation/` habría detectado; la evidencia de
  validación es incompleta.
- **Prioridad**: Media.
- **Cuándo abordarlo**: cuando se defina el runner común de migraciones/CI
  para ejecutar `validation/` tras cada migración en Postgres desechable.

## 4. Verificación/checksum de `schema_migrations` — solo versión

- **Problema**: `schema_migrations` registra `version` (y aplica/revierte por
  prefijo `NNN_`), pero no almacena ni verifica un hash del contenido de la
  migración aplicada (ver `MigrationRunner.cs`).
- **Riesgo**: si una migración ya aplicada cambia (p. ej. por una edición
  posterior o conflicto de merge), no se detecta la divergencia entre lo
  aplicado y lo que el repo dice que se aplicó.
- **Prioridad**: Media.
- **Cuándo abordarlo**: al fortalecer el runner de migraciones (misma tanda que
  #3): añadir columna checksum y fail en divergencia.

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

## 7. SonarCloud — sin cobertura de tests en el análisis automático

- **Problema**: SonarCloud se integra por análisis **automático** (GitHub App,
  configurado solo con `sonar.cpd.exclusions=database/baseline/` en
  `.sonarcloud.properties`). El análisis automático no ejecuta tests, por lo
  que **Coverage aparece vacío** y no hay reporte de cobertura. No hay scanner
  de SonarCloud en CI (`.github/workflows`) ni `sonar-project.properties`, ni
  configuración de coverlet/cobertura en los proyectos .NET ni `@vitest/coverage-*`
  en el frontend (el builder de tests usa Vitest v4.1.11 sin proveedor de
  cobertura instalado).
- **Riesgo**: la métrica de calidad de SonarCloud está incompleta (sin
  cobertura); no se puede exigir una Quality Gate de cobertura y no hay
  evidencia automática de qué código probado/desprobado hay.
- **Prioridad**: Media.
- **Cuándo abordarlo**: en un PR propio de CI (fuera de este de estándares),
  que necesitará: (a) añadir el scanner de SonarCloud con `SONAR_TOKEN` como
  secreto del repo (hoy no existe en workflows), (b) recopilar cobertura .NET
  (coverlet, formato opencover) y frontend (`@vitest/coverage-v8`, reporte
  lcov) y subirla al scanner. Requiere decisión del mantenedor sobre secretos
  y modo de análisis (automático vs. CI).

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

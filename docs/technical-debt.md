# Deuda técnica registrada — SchoolManager

Estado consolidado después del Bloque 042 / PR #97, actualizado el 2026-09-16.
Este archivo enumera únicamente deuda técnica **real y verificable**. El historial
de deudas cerradas se conserva al final para no volver a abrir problemas ya
resueltos.

> Regla: no registrar deuda especulativa. Toda entrada debe salir de evidencia
> observable en código, CI, producción o documentación operativa.

## Estado base verificado

- `main`: merge commit `20bea4f9dd346836205620dce2add6f60d17b372` (PR #97).
- Migraciones activas del repositorio: `001` → `039`.
- Producción: `schema_migrations` contiene `baseline-001-fase1a` y las migraciones
  numéricas `007` → `039`.
- Configuración actual de producción: `multiples_instituciones = false`, una
  institución activa y un `platform_admin` activo.
- Arquitectura vigente: Angular → API .NET → PostgreSQL/Supabase/RPC; Supabase
  directo en frontend queda reservado a Auth.

---

## Deuda abierta

### #5. Observabilidad de producción — parcial

- **Estado:** PARCIAL.
- **Ya resuelto:** `GET /health` como liveness y `GET /health/ready` como
  readiness real contra PostgreSQL (`SELECT 1`, 200/503, sin secretos).
- **Pendiente verificable:** el backend sigue usando logging de consola de
  ASP.NET Core sin logging estructurado, correlación consistente por request,
  métricas propias ni alertas de aplicación.
- **Riesgo:** diagnóstico lento y detección tardía de degradaciones que no
  derriban completamente el proceso o la base.
- **Prioridad:** Media.
- **Criterio de cierre:** logging estructurado mínimo con `trace/request id`,
  contexto seguro de ruta/usuario cuando aplique, y una estrategia de métricas
  y alertas que no exponga datos sensibles.
- **Referencia:** `docs/observabilidad.md`.

### #11. Mapeo seguro y amigable de errores de negocio

- **Estado:** PARCIAL.
- **Evidencia:** `ApiControllerBase` ya centraliza `ToError` y traduce múltiples
  constraints conocidas (`23505`, `23514`, etc.) a mensajes de negocio. Sin
  embargo, cuando una constraint no está en `MensajesRestricciones`,
  `MensajeError` devuelve `PostgresException.MessageText`; por tanto aún puede
  llegar texto técnico de PostgreSQL a la UI.
- **Riesgo:** mensajes inconsistentes, acoplamiento accidental a detalles de DB
  y posible exposición de información técnica innecesaria.
- **Prioridad:** Media-Alta por ser un cambio pequeño y transversal.
- **Criterio de cierre:** mantener PostgreSQL/RPC como autoridad, ampliar el mapa
  solo para restricciones reales y usar un fallback seguro por familia de
  error; agregar tests que demuestren que no se pierde la semántica 400/403/404/
  409 ni se muestra texto técnico no controlado.

### #12. Auditoría de hallazgos `High` históricos de Sonar Overall Code

- **Estado:** ABIERTA.
- **Evidencia:** el Quality Gate de código nuevo está verde y SonarScanner for
  .NET analiza C# + TypeScript con cobertura real, pero la documentación vigente
  aún registra hallazgos `High` del Overall Code pendientes de clasificación.
- **Riesgo:** mezclar deuda histórica, falso positivo y vulnerabilidad real sin
  una decisión documentada.
- **Prioridad:** Media.
- **Criterio de cierre:** revisar cada `High`, clasificarlo con evidencia y
  corregir únicamente los hallazgos reales sin bajar reglas, thresholds ni
  excluir código para forzar verde.

### #13. E2E autenticado completo en staging seguro

- **Estado:** ABIERTA.
- **Evidencia:** Playwright y smoke no autenticado existen; `auth.spec.ts` está
  preparado pero condicionado a variables de staging. No existe aún un entorno
  completo aislado con Supabase + API + frontend de staging y credenciales de
  prueba.
- **Riesgo:** los flujos de login, permisos, aislamiento institucional y CRUD
  real no se validan de extremo a extremo de forma automatizada.
- **Prioridad:** Media.
- **Criterio de cierre:** ejecutar el plan `E2E-01` → `E2E-06` de
  `docs/testing/e2e-staging-plan.md`, sin reutilizar producción ni secretos de
  usuarios reales.

### #14. Prueba de carga controlada del backend

- **Estado:** ABIERTA — issue #85.
- **Evidencia:** existe baseline del frontend/CDN, pero no una medición
  equivalente documentada para la API .NET/Render ni para una ruta autenticada
  de solo lectura que atraviese PostgreSQL/Supabase.
- **Riesgo:** no existe un punto de referencia real de capacidad y degradación
  extremo a extremo del backend.
- **Prioridad:** Media.
- **Criterio de cierre:** baseline de `/health`, escalado progresivo seguro y
  prueba autenticada read-only con identidad exclusiva de pruebas, documentando
  latencia, errores y umbral aceptable.

---

## Deuda resuelta / decisiones cerradas

### #1. Guards y permisos frontend — RESUELTA

`PermissionGuard` y la barrera de inicialización de sesión están en uso. Ciclos
usa `academico.ciclos.ver`, Estructura Académica usa
`academico.estructura.ver` y Seguridad y acceso exige capacidades de identidad o
`platform_admin`. `AdminGuard`/`PadreGuard` no deben reintroducirse.

### #2. Grados y jornadas multiinstitución — RESUELTA

Migraciones 020/025: `institucion_id`, unicidad por institución, FK compuestas y
RLS/RPC contextualizados.

### #3. Ejecución automática de validaciones SQL — RESUELTA

`MigrationValidationSuiteTests`/`ValidationRunner` ejecutan las validaciones de
migraciones en base desechable.

### #4. Checksums de migraciones — RESUELTA

`MigrationRunner` valida SHA-256 y falla ante divergencias de una migración ya
registrada.

### #6. Backend de pagos/cobranza — RESUELTA

Bloque 021: pagos, aplicaciones, saldo derivado, sincronización de cargos y
anulación transaccional.

### #7. SonarCloud representativo — RESUELTA

SonarScanner for .NET analiza backend C# y frontend TypeScript, importa Cobertura
+ LCOV y el Quality Gate forma parte del CI.

### #8. Duplicación corregible — RESUELTA / residual aceptado

Se extrajo `ApiControllerBase`, se eliminó código muerto y la duplicación
estructural deliberada quedó documentada.

### #9. Cobertura y gate — RESUELTA como guardrail

Existe baseline versionado y gate anti-regresión; Sonar cubre New Code. Mejorar
cobertura sigue siendo deseable, pero ya no es una deuda sin control.

### #10. Acceso directo de negocio a Supabase — RESUELTA

PR #53 fue fusionado a `main` (`61f77362ecc1830cccbf8e11d6a3b3ef1a414ce9`).
Los módulos de negocio del frontend pasan por la API .NET; `auth.ts` conserva
Supabase Auth como excepción deliberada.

### Bloque 042: selector/contexto multiinstitución — RESUELTO

El AppShell resuelve automáticamente una institución en modo single y muestra
selector cuando existen varios contextos visibles. La selección se persiste por
usuario y `platform_admin` puede operar con contexto administrable sin convertirlo
en membresía institucional.

### Bloque 042: divergencia `academico.*` / `configuracion.*` — RESUELTA

La migración 039 centraliza el puente canónico dentro de
`usuario_tiene_permiso_actual`, manteniendo las RPC históricas sin exigir aliases
internos ocultos a roles institucionales dinámicos.

---

## No clasificar como deuda técnica

- **Crear/invitar usuarios nuevos:** es funcionalidad pendiente del roadmap de
  identidad, no reparación de una deficiencia existente. Debe preservar
  `Persona` global, `Usuario` global y vinculación explícita de Auth.
- **Modo multiinstitución desactivado hoy:** es configuración de producción
  (`multiples_instituciones=false`), no una limitación; el código 042 soporta
  ambos modos.

## Convenciones

- Nueva deuda confirmada → agregar una entrada con evidencia, riesgo, prioridad y
  criterio de cierre; crear issue cuando requiera seguimiento independiente.
- Deuda cerrada → mover a **Deuda resuelta / decisiones cerradas** con referencia
  al PR, migración o prueba que la cerró.
- No convertir funcionalidades futuras en “deuda” solo porque todavía no están
  implementadas.

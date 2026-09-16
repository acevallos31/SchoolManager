# Deuda técnica registrada — SchoolManager

Estado consolidado después de los Bloques 042, 043A, 043B, 044 y 045A, actualizado el 2026-09-16.
Este archivo enumera únicamente deuda técnica **real y verificable**. El historial
de deudas cerradas se conserva al final para no volver a abrir problemas ya
resueltos.

> Regla: no registrar deuda especulativa. Toda entrada debe salir de evidencia
> observable en código, CI, producción o documentación operativa.

## Estado base verificado

- Base funcional previa a 045A: merge `13e6771c0248cf82c7b97a2df05fad483b45a47b`
  (PR #105 / Bloque 044E).
- Migraciones activas del repositorio: `001` → `039`.
- Última verificación read-only de producción: `schema_migrations` contiene
  `baseline-001-fase1a` y las migraciones numéricas `007` → `039`.
- Configuración observada en esa verificación: `multiples_instituciones = false`,
  una institución activa y un `platform_admin` activo.
- Arquitectura vigente: Angular → API .NET → PostgreSQL/Supabase/RPC; Supabase
  directo en frontend queda reservado a Auth.
- CI post-merge de 044E terminó verde, incluyendo Sonar Quality Gate y despliegue
  de producción.

---

## Deuda abierta

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

### #5. Observabilidad de producción — RESUELTA

Bloque 045A completa la parte pendiente sobre los health checks existentes:

- `JsonConsole` nativo de ASP.NET Core produce logs estructurados JSON a stdout;
- `RequestObservabilityMiddleware` agrega `RequestId`, `TraceId`, patrón de ruta,
  estado autenticado y claim `sub` cuando aplica;
- cada respuesta expone `X-Request-ID` para correlación operativa;
- excepciones no controladas devuelven `ProblemDetails` 500 sin detalle interno;
- el middleware no registra query strings, headers, body, tokens, cookies,
  contraseñas, correos ni nombres;
- `ApiObservabilityMetrics` instrumenta requests, 5xx y duración mediante
  `System.Diagnostics.Metrics`, sin endpoint público ni dependencia SaaS;
- `docs/observabilidad.md` define umbrales y estrategia de alertas para
  readiness, liveness, tasa 5xx y p95 de latencia.

Agregar un exporter OpenTelemetry/Prometheus o una plataforma externa en el futuro
es una decisión operativa opcional; la instrumentación y el contrato de
correlación ya quedan en el backend.

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

### #11. Mapeo seguro y amigable de errores de negocio — RESUELTA

PR #99 (`d622a1f50f3e78b00a9e83f2ae3d80c574e80a5a`) cerró el fallback que
podía exponer `PostgresException.MessageText` para errores no reconocidos.
`ApiControllerBase` conserva mensajes específicos para constraints conocidas,
normaliza familias/SQLSTATE con fallback seguro y mantiene la semántica HTTP
400/403/404/409. `P0001` conserva el mensaje de negocio controlado por las RPC.
Se agregaron pruebas específicas para errores conocidos, desconocidos y fallback.

### #12. GitHub Actions sobre runtime Node 24 — RESUELTA

PR #100 (`e46c01343f3ac9ace3fbce29ca8ba4237ffc156f`) migró las actions oficiales
que todavía apuntaban a Node.js 20 hacia releases con runtime Node 24, fijadas por
SHA: `checkout` v7.0.1, `setup-dotnet` v6.0.0, `setup-node` v7.0.0,
`upload-artifact` v7.0.1 y `download-artifact` v8.0.1. El CI completo y Sonar
Quality Gate pasaron; upload/download de coberturas funcionó y desapareció el
warning específico `Node.js 20 is deprecated` para esas actions.

> Nota: `download-artifact` v8.0.1 puede emitir un warning upstream distinto,
> `DEP0005 Buffer() is deprecated`. No es el warning de runtime Node 20 que
> motivó 043B y no rompe el workflow; si persiste en futuras releases se evalúa
> como dependencia upstream, no como reapertura automática de #12.

### #13. E2E autenticado completo en staging seguro — RESUELTA

Bloques 044A–044E cerraron la brecha sin usar producción ni infraestructura cloud
adicional:

- configuración Angular `staging` y manifest runtime con allowlist anti-producción;
- backend `Staging` con fail-fast para JWT/PostgreSQL/CORS;
- Supabase local efímero y migraciones reconstruidas desde las fuentes canónicas;
- seed local de identidades/RBAC con credenciales efímeras y sin `platform_admin`;
- Auth real contra Supabase local + API .NET + frontend Angular;
- casos negativos de login, 401/403 y aislamiento entre dos instituciones;
- dataset académico mínimo con ciclo, período, grado, jornada, sección, alumno y
  matrícula;
- workflow `E2E Authenticated Regression` reutilizable por `workflow_call`, manual
  con `workflow_dispatch` y nocturno con `schedule`;
- artifacts de Playwright (reporte HTML, traces/screenshots) ante fallos y cleanup
  obligatorio del entorno efímero.

Los workflows puntuales de 044C/044D fueron sustituidos por la regresión mantenible
044E. El E2E completo no requiere secretos de producción ni datos reales.

### Hallazgos `High` históricos de Sonar — RESUELTOS

La auditoría ya había ocurrido antes de 042 y no debía permanecer como deuda
abierta. PR #71 (`3835a59c86217534b6560a1d47bdcb5d9166f00d`) corrigió los
hallazgos visibles de seguridad/duplicación sin bajar el Quality Gate. PR #72
(`d4b654f333eeb9938b38db62d794a4266b1ab277`) cerró explícitamente el último
issue de seguridad `High` en `scripts/check-coverage-gate.py`; su análisis Sonar
terminó con Quality Gate verde y ratings A.

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

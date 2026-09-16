# HANDOFF — cierre post-043B y deuda técnica consolidada

## Fecha
2026-09-16 (UTC-6).

## Base funcional verificada

- Bloque 042 / PR #97: **MERGEADO**.
- 043A / PR #99 `fix(api): 043A - normalizar errores de negocio`: **MERGEADO**
  en `d622a1f50f3e78b00a9e83f2ae3d80c574e80a5a`.
- 043B / PR #100 `chore(ci): 043B - migrar GitHub Actions a Node 24`:
  **MERGEADO** en `e46c01343f3ac9ace3fbce29ca8ba4237ffc156f`.
- CI de PR #99: **SUCCESS**, incluyendo Sonar Quality Gate.
- CI de PR #100: **SUCCESS**, incluyendo subida/descarga de artifacts y Sonar
  Quality Gate.
- Los logs de 043B ya no contienen el warning específico
  `Node.js 20 is deprecated` para las actions migradas.
- Rama de este cierre documental: `docs/deuda-tecnica-post-042`.

## Estado real de producción

La última verificación read-only documentada del 2026-09-16 confirmó:

- `schema_migrations` contiene `baseline-001-fase1a`;
- migraciones numéricas registradas `007` → `039`;
- `multiples_instituciones = false`;
- exactamente 1 institución activa;
- exactamente 1 asignación activa de `platform_admin`.

Durante 043A, 043B y este cierre documental no se ejecutaron migraciones ni
escrituras en Supabase/producción.

## Bloque 042 — estado cerrado

Quedó integrado en `main`:

- roles institucionales dinámicos;
- rol global protegido `platform_admin`;
- autoridad institucional estricta e aislamiento entre instituciones;
- crear/clonar/editar/desactivar roles institucionales;
- reemplazar permisos de un rol;
- asignar y retirar roles de usuarios;
- protección del último administrador institucional y del último
  Superadministrador;
- API `/api/configuracion/seguridad`;
- UI **Configuración → Seguridad y acceso**;
- directorio de usuarios existentes con búsqueda, estado, identidad vinculada y
  roles de la institución activa;
- `platform_admin` puede consultar el directorio global manteniendo las
  asignaciones institucionales filtradas por el contexto operativo;
- compatibilidad real con modo monoinstitución y multiinstitución;
- selector de institución en AppShell cuando hay varios contextos visibles;
- resolución automática de la única institución en modo single;
- persistencia del contexto por usuario;
- migración 039 como puente canónico entre permisos de aplicación
  `academico.*` y aliases internos históricos `configuracion.*`.

## Arquitectura vigente

- Angular → API .NET → PostgreSQL/Supabase/RPC.
- Supabase directo en frontend únicamente para autenticación (`auth.ts`).
- `PermissionGuard` es el guard vigente.
- `AdminGuard`/`PadreGuard` no deben reintroducirse.
- Autorización .NET y RLS/RPC siguen siendo capas separadas.
- Los roles institucionales son dinámicos; los permisos de plataforma no son
  delegables a una institución.
- `Persona` y `Usuario` son identidades globales; las asignaciones de rol pueden
  tener ámbito institucional.

## Cierre 043A — errores de negocio seguros

PR #99 quedó integrado y cerró la exposición accidental de texto técnico de
PostgreSQL:

- constraints conocidas conservan mensajes de negocio específicos;
- constraints/SQLSTATE no reconocidos usan fallback seguro;
- no se devuelve `PostgresException.MessageText` crudo como fallback genérico;
- mensajes `P0001` controlados por RPC se mantienen como mensajes de negocio;
- se preserva la semántica HTTP 400/403/404/409;
- las pruebas de integración cubren errores conocidos y desconocidos.

## Cierre 043B — GitHub Actions sobre Node 24

PR #100 quedó integrado con las actions oficiales fijadas por SHA:

- `actions/checkout` v7.0.1;
- `actions/setup-dotnet` v6.0.0;
- `actions/setup-node` v7.0.0;
- `actions/upload-artifact` v7.0.1;
- `actions/download-artifact` v8.0.1.

El pipeline completo pasó, incluidos artifacts de cobertura y Sonar Quality
Gate. El warning de compatibilidad `Node.js 20 is deprecated` que motivó el
bloque desapareció.

`download-artifact` v8.0.1 todavía puede imprimir un warning upstream distinto
`DEP0005 Buffer() is deprecated`; no es el warning de runtime Node 20 y no afecta
el resultado del workflow. Se observará en futuras releases upstream sin tratarlo
como reapertura automática de 043B.

## Deuda técnica realmente abierta

1. **E2E autenticado en staging:** Playwright existe, pero falta un entorno
   aislado Supabase + API + frontend y sus identidades de prueba.
2. **Observabilidad adicional:** faltan logging estructurado, correlación,
   métricas y alertas más allá de `/health` y `/health/ready`.
3. **Prueba de carga backend:** issue #85; falta baseline seguro de la API y una
   lectura autenticada end-to-end.

El registro canónico está en `docs/technical-debt.md`.

## Deudas que ya NO deben aparecer como pendientes

- errores PostgreSQL no controlados hacia UI: cerrado por 043A / PR #99;
- GitHub Actions apoyadas en runtime Node 20: cerrado por 043B / PR #100;
- hallazgos `High` históricos de Sonar: resueltos por PR #71/#72;
- acceso directo de negocio a Supabase (#10): resuelto por PR #53;
- selector global multiinstitución: implementado por 042;
- divergencia `academico.*` / `configuracion.*`: resuelta por migración 039;
- análisis C# real en Sonar y cobertura importada: resuelto;
- pagos/cobranza: resuelto;
- grados/jornadas multiinstitución: resuelto;
- validaciones y checksum de migraciones: resueltos.

## Siguiente bloque técnico recomendado

### 044 — E2E autenticado en staging aislado

Objetivo:

1. preparar un entorno de staging separado de producción;
2. usar Supabase, API y frontend de staging con secretos/identidades de prueba;
3. ejecutar el plan `E2E-01` → `E2E-06` de
   `docs/testing/e2e-staging-plan.md`;
4. validar login, contexto institucional, permisos y CRUD real sin datos de
   producción;
5. incorporar el resultado al CI solo cuando el entorno sea reproducible y
   seguro;
6. no usar usuarios reales ni pruebas destructivas en producción.

La observabilidad estructurada y la prueba de carga backend quedan como bloques
posteriores e independientes.

## Funcionalidad futura — no deuda

El siguiente bloque funcional de identidad puede incorporar **Crear/Invitar
usuario**, pero debe permanecer separado del hardening anterior. Debe:

- buscar/reutilizar explícitamente una `Persona` existente o crear una nueva;
- crear/reutilizar `Usuario` global sin duplicar identidad;
- asignar rol en la institución activa;
- dejar la cuenta pendiente de invitación/vinculación cuando corresponda;
- ejecutar cualquier operación privilegiada de Supabase Auth desde backend
  seguro, nunca desde Angular;
- no vincular identidades automáticamente solo por coincidencia de correo.

## Inconsistencia documental pendiente

`README.md` continúa describiendo el proyecto como si llegara solo a migración
018 y como si pagos/portal no tuvieran backend. Esa información ya no coincide
con el estado canónico. Debe sincronizarse en un cambio documental separado o
ampliando explícitamente el alcance de una futura tarea documental.

## Reglas operativas

- Siempre rama; no escribir directo a `main`.
- No force push.
- No secretos en repo.
- No E2E destructivo en producción.
- No aplicar migraciones fuera de orden.
- No ejecutar migraciones ni cambios de datos reales sin autorización explícita.
- Validar con CI/Quality Gate antes de cualquier merge.
- `AGENTS.md` sigue siendo la referencia operativa del repositorio.

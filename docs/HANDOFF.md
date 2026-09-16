# HANDOFF — cierre post-042 y deuda técnica consolidada

## Fecha
2026-09-16 (UTC-6).

## Base verificada

- `main`: `20bea4f9dd346836205620dce2add6f60d17b372`.
- PR #97 `feat(rbac): 042 - roles dinámicos institucionales y Superadministrador`: **MERGEADO**.
- CI/CD run #656 sobre `main`: **SUCCESS**.
- Render desplegó el mismo commit y quedó `live`.
- Vercel reportó deployment `success` para el mismo commit.
- Rama de esta actualización documental: `docs/deuda-tecnica-post-042`.

## Estado real de producción

La verificación read-only del 2026-09-16 confirma:

- `schema_migrations` contiene `baseline-001-fase1a`;
- migraciones numéricas registradas `007` → `039`;
- `multiples_instituciones = false`;
- exactamente 1 institución activa;
- exactamente 1 asignación activa de `platform_admin`.

Durante esta actualización documental no se ejecutaron migraciones ni escrituras
en Supabase/producción.

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

## Deuda técnica realmente abierta

1. **Errores de negocio amigables:** `main` aún permite que una constraint no
   reconocida termine mostrando `PostgresException.MessageText`. PR #99 prepara
   el cierre con fallback seguro y tests; sigue sin merge.
2. **GitHub Actions / Node.js 20:** CI muestra warnings explícitos porque
   `checkout@v4`, `setup-dotnet@v4`, `setup-node@v4`, `upload-artifact@v4` y
   `download-artifact@v4` apuntan a Node 20 y GitHub los fuerza temporalmente a
   Node 24. Deben migrarse a releases oficiales con runtime Node 24.
3. **E2E autenticado en staging:** Playwright existe, pero falta el entorno
   aislado Supabase + API + frontend y sus identidades de prueba.
4. **Observabilidad adicional:** faltan logging estructurado, correlación,
   métricas y alertas más allá de `/health` y `/health/ready`.
5. **Prueba de carga backend:** issue #85; falta baseline seguro de la API y una
   lectura autenticada end-to-end.

El registro canónico actualizado está en `docs/technical-debt.md`.

## Hallazgo corregido durante esta auditoría

La documentación anterior decía que los issues `High` históricos de Sonar
seguían pendientes. Eso era obsoleto:

- PR #71 (`3835a59c86217534b6560a1d47bdcb5d9166f00d`) corrigió los hallazgos
  visibles de seguridad y duplicación sin bajar el Quality Gate;
- PR #72 (`d4b654f333eeb9938b38db62d794a4266b1ab277`) cerró explícitamente el
  último issue de seguridad `High` del coverage gate;
- el análisis posterior quedó verde con ratings A.

Por tanto, Sonar `High` histórico ya **no** se clasifica como deuda abierta.

## Deudas que ya NO deben aparecer como pendientes

- acceso directo de negocio a Supabase (#10): resuelto y mergeado por PR #53;
- selector global multiinstitución: implementado por 042;
- divergencia `academico.*` / `configuracion.*`: resuelta por migración 039;
- análisis C# real en Sonar y cobertura importada: resuelto;
- hallazgos `High` históricos de Sonar: resueltos por PR #71/#72;
- pagos/cobranza: resuelto;
- grados/jornadas multiinstitución: resuelto;
- validaciones y checksum de migraciones: resueltos.

## Trabajo preparado y todavía sin merge

### PR #99 — 043A, normalización final de errores de negocio

La rama `fix/043a-errores-negocio-seguros` ya contiene:

- mensajes específicos para constraints conocidas;
- fallback seguro por SQLSTATE sin exponer texto técnico de PostgreSQL;
- conservación de mensajes `P0001` controlados por RPC;
- preservación de status HTTP 400/403/404/409;
- `ConfiguracionController` mantiene `{ error, code }` usando el normalizador;
- pruebas específicas de errores conocidos/desconocidos.

CI #658 quedó verde: API 206/206, DB 207/207, frontend 374/374 y Sonar Quality
Gate PASS. El PR permanece abierto y **no debe mergearse sin autorización**.

## Siguiente bloque técnico recomendado

### 043B — GitHub Actions sobre runtime Node 24

Objetivo mínimo y seguro:

1. reemplazar los majors antiguos de las actions oficiales que disparan el
   warning de Node 20;
2. usar releases oficiales compatibles con Node 24;
3. mantener intactos scripts, secretos, condiciones y Quality Gate;
4. ejecutar el pipeline completo;
5. confirmar en logs que desaparece `Node.js 20 is deprecated`;
6. no mezclar este hardening con cambios funcionales.

Después de 043B, staging E2E queda como el siguiente bloque de infraestructura
con mayor valor de verificación.

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

## Inconsistencia documental detectada fuera de este cambio

`README.md` continúa describiendo el proyecto como si llegara solo a migración
018 y como si pagos/portal no tuvieran backend. Esa información ya no coincide
con `main`. No se modifica en este checkpoint para mantener el alcance de la
limpieza en `technical-debt.md`, `HANDOFF.md` y `AI_CONTEXT.md`; debe actualizarse
en un cambio documental separado o ampliando explícitamente el alcance.

## Reglas operativas

- Siempre rama; no escribir directo a `main`.
- No force push.
- No secretos en repo.
- No E2E destructivo en producción.
- No aplicar migraciones fuera de orden.
- No ejecutar migraciones ni cambios de datos reales sin autorización explícita.
- Validar con CI/Quality Gate antes de cualquier merge.
- `AGENTS.md` sigue siendo la referencia operativa del repositorio.

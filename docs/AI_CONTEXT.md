# SchoolManager — AI Context

## Estado general

- Proyecto web de gestión escolar en Angular + .NET + PostgreSQL/Supabase.
- Arquitectura objetivo y vigente: Angular → API .NET → PostgreSQL/Supabase/RPC.
- Supabase directo en frontend queda reservado a autenticación (`auth.ts`).
- Deuda #10 de accesos directos de negocio a Supabase: **RESUELTA y mergeada**
  en Bloque 030 / PR #53.
- Bloque 042 de RBAC dinámico institucional y Superadministrador: **RESUELTO y
  mergeado** en PR #97 (`20bea4f9dd346836205620dce2add6f60d17b372`).
- Producción está en modo **monoinstitución** (`multiples_instituciones=false`)
  con una institución activa, pero la implementación 042 soporta también modo
  multiinstitución.
- Migraciones activas del repositorio: `001` → `039`.

## Arquitectura

- Angular 22 standalone frontend.
- Backend ASP.NET Core Web API sobre .NET 10.
- PostgreSQL/Supabase como persistencia.
- Monolito modular; evitar CQRS, MediatR, microservicios, Generic Repository y
  UnitOfWork artificial.
- UUID para PK/FK internos.
- RLS + RPC para invariantes y escrituras críticas.
- Autorización por permisos, no checks hardcodeados por rol.
- Sin DELETE físico de históricos.
- Autorización .NET y autorización/RLS interna de PostgreSQL son capas separadas.

### Autenticación e identidad

- El frontend autentica contra Supabase Auth en `auth.ts`.
- El backend valida el JWT Supabase.
- `GET /api/auth/me` entrega identidad, roles, permisos y contexto institucional
  disponible.
- No existe ni debe agregarse `POST /api/auth/login` en .NET.
- `Persona` es global.
- `Usuario` es global y puede vincularse explícitamente a una identidad Auth.
- No vincular identidades automáticamente solo por coincidencia de correo.
- Los roles/permisos viven en SchoolManager; no se delegan al proveedor OAuth.

## Contexto institucional

- **Modo single:** una única institución activa se resuelve automáticamente.
- **Modo multi:** el usuario debe trabajar con un contexto institucional
  explícito entre los contextos autorizados/administrables.
- El AppShell muestra la institución directamente cuando solo existe una y un
  selector cuando existen varias instituciones visibles.
- La selección se persiste por usuario en `ContextoInstitucionService`.
- `platform_admin` puede recibir instituciones administrables sin que eso cree
  una membresía institucional ni conceda permisos locales implícitos.
- Solo instituciones activas pueden seleccionarse como contexto operativo.
- Grados y jornadas son por institución desde 020; secciones validan el mismo
  contexto mediante FK compuestas.

### Estado actual de producción verificado 2026-09-16

Consulta read-only en Supabase:

- `schema_migrations`: `baseline-001-fase1a` + migraciones numéricas `007` →
  `039`;
- `multiples_instituciones = false`;
- 1 institución activa;
- 1 asignación activa de `platform_admin`.

No inferir este estado para otros entornos: staging/local deben verificarse por
separado.

## RBAC / Bloque 042

Quedó integrado en `main`:

- roles institucionales dinámicos;
- plantillas base y roles institucionales editables;
- rol global protegido `platform_admin` (Superadministrador);
- permisos con ámbito y delegabilidad explícitos;
- autoridad institucional estricta;
- aislamiento entre instituciones;
- crear/clonar/editar/desactivar roles;
- reemplazar permisos de roles;
- asignar y retirar roles de usuarios;
- protección del último administrador institucional y del último
  Superadministrador;
- consultas de Seguridad y acceso;
- directorio administrativo de usuarios existentes;
- UI `/configuracion/seguridad-acceso`;
- `PermissionGuard` acepta permisos concretos, `permisosCualquiera` y excepción
  explícita para Superadministrador en Seguridad y acceso.

### Namespaces de permisos

La aplicación usa permisos canónicos `academico.*` para ciclos/estructura. RPC
históricas conservan aliases internos `configuracion.*`.

La migración 039 resuelve esa diferencia en
`usuario_tiene_permiso_actual`: un alias interno puede satisfacerse con su
permiso canónico equivalente, manteniendo el mismo `institucion_id`. No volver a
asignar aliases internos ocultos a roles institucionales solo para atravesar la
capa DB.

## Administración de usuarios

La pantalla Seguridad y acceso administra **usuarios internos existentes**:

- búsqueda por nombre/correo;
- estado de usuario;
- identidad Auth vinculada/no vinculada;
- roles en la institución activa;
- asignación y retiro de roles según capacidades.

`platform_admin` puede consultar el directorio global, pero las asignaciones de
roles institucionales siguen filtradas por el contexto operativo.

### Funcionalidad pendiente de roadmap: Crear/Invitar usuario

No es deuda técnica; es funcionalidad nueva. Debe preservar:

1. búsqueda/reutilización explícita de una `Persona` existente;
2. creación de `Persona` solo si corresponde;
3. creación/reutilización de `Usuario` global;
4. asignación de rol en la institución activa;
5. estado pendiente de invitación/vinculación cuando no exista identidad Auth;
6. operaciones privilegiadas de Supabase Auth únicamente desde backend seguro;
7. ninguna vinculación automática por coincidencia de correo.

## Modelo académico

Institución → Ciclo → Período matrícula → Grado → Jornada opcional → Sección →
Matrícula → Alumno.

- Los períodos pueden ser anticipados, normales o extraordinarios.
- Fechas de matrícula y fechas académicas son independientes.
- Una sección con matrículas no cambia ciclo, grado ni jornada.
- Una matrícula `anulada` libera `(alumno_id, ciclo_id)` para rematrícula.
- `pendiente`, `activa`, `finalizada`, `retirada` y `trasladada` siguen
  bloqueando una segunda matrícula del mismo alumno/ciclo.
- Se conserva historial; no se revive ni elimina la fila anulada.

## Finanzas

### Cargos

- Tabla `cargos` desde 019.
- Permisos `academico.cargos.{ver,generar,anular}`.
- `vencido` es derivado por fecha.
- Estados `parcial`/`pagado` se sincronizan con 021 mediante triggers.
- API: `CargosController`.
- Frontend `/cargos` consume API .NET.

### Pagos / cobranza

- `pagos` + `pagos_aplicaciones` desde 021.
- Un pago puede aplicarse a varios cargos.
- Saldo siempre derivado, nunca almacenado.
- Sin sobrepago.
- Anulación atómica con trazabilidad.
- Permisos `academico.pagos.{ver,registrar,anular}`.
- Frontend `/pagos` consume API .NET.

## Portal Responsable

- Migración 022 expone lectura por identidad responsable → alumno.
- RPC principales: alumnos, cargos, resumen financiero, pagos y aplicaciones.
- `/portal-padre` es read-only y queda fuera del AppShell administrativo.
- Sin botón de pago.

## Migraciones

Todas las migraciones activas `001` → `039` están en `main` y el test de orden
espera exactamente esa cadena.

### Resumen por bloques

- `001`–`009`: convención, institución, personas, responsables, contexto
  académico, RBAC base, normalización y RLS/RPC.
- `010`–`018`: identidad, alumno/documento, configuración de implementación,
  centro educativo, ciclos/períodos, estructura académica, responsables y
  configuración financiera.
- `019`: cargos/obligaciones.
- `020`: grados/jornadas multiinstitución.
- `021`: pagos/cobranza.
- `022`: portal responsable read-only.
- `023`: permisos de aplicación `academico.ciclos.*`.
- `024`: permisos de aplicación `academico.estructura.*`.
- `025`: corrección de unicidad de grados/jornadas por institución.
- `026`: rematrícula tras anulación.
- `027`: vinculación explícita de identidad OAuth.
- `028`: roles dinámicos institucionales.
- `029`: operaciones sobre roles institucionales.
- `030`: clonado/edición de roles institucionales.
- `031`: invariantes de roles institucionales.
- `032`: reemplazo de permisos de rol.
- `033`: asignación de roles institucionales.
- `034`: desactivación de roles institucionales.
- `035`: protección del último administrador/Superadministrador.
- `036`: autoridad institucional estricta.
- `037`: lectura institucional estricta.
- `038`: consulta Seguridad y acceso.
- `039`: canonicalización de permisos de configuración académica.

### Regla operativa de migraciones

- No reescribir migraciones ya aplicadas.
- Cada migración activa mantiene rollback y validation según la convención.
- Producción se actualiza manualmente después de revisión/CI.
- No ejecutar migraciones automáticamente desde agentes.

## Arquitectura API / Bloque 030

Bloque 030 está cerrado y mergeado mediante PR #53.

- Alumnos, Matrículas, Ciclos/Períodos, Estructura Académica y Configuración
  pasan por API .NET.
- Cero accesos directos Supabase de negocio en frontend.
- Única excepción productiva: Supabase Auth en `auth.ts`.
- Reglas e invariantes permanecen en RPC/DB.
- CI mantiene una verificación de frontera API del frontend.

## Frontend / UX

- Foundation visual `sm-*` y AppShell global integrados.
- Drawer móvil, accesibilidad básica, modales, focus handling y scroll lock
  cerrados en el bloque UI/UX.
- `/login` y `/portal-padre` quedan fuera del AppShell.
- `PermissionGuard` es el guard de navegación vigente.
- `AdminGuard`/`PadreGuard` fueron eliminados; no reintroducirlos.

Rutas principales:

- `/dashboard`
- `/alumnos`
- `/matriculas`
- `/responsables`
- `/cargos`
- `/pagos`
- `/configuracion`
- `/configuracion/seguridad-acceso`
- `/configuracion/ciclos`
- `/configuracion/estructura-academica`
- `/configuracion/conceptos-financieros`
- `/configuracion/planes-pago`
- `/portal-padre`

## Calidad / CI / Sonar

- `.github/workflows/deploy.yml` valida backend, API tests, DB tests, frontend,
  cobertura y Sonar.
- SonarScanner for .NET analiza C# real + TypeScript.
- Cobertura backend Cobertura y frontend LCOV se importan.
- Quality Gate forma parte del CI y no debe forzarse reduciendo thresholds ni
  excluyendo archivos para ocultar hallazgos.
- Deudas históricas #7 y #9 de Sonar/cobertura están resueltas como guardrails.
- Los hallazgos `High` históricos de Sonar también están **resueltos**: PR #71
  limpió los hallazgos visibles de seguridad/duplicación y PR #72 cerró el
  último issue `High` del coverage gate; ambos fueron mergeados y el análisis
  posterior quedó verde con ratings A.
- El run #656 sobre el merge de PR #97 terminó `success`.
- Deuda actual del workflow: varias actions oficiales siguen en majors que
  apuntan a Node.js 20 (`checkout@v4`, `setup-dotnet@v4`, `setup-node@v4`,
  `upload-artifact@v4`, `download-artifact@v4`). GitHub las está forzando
  temporalmente a Node 24 y emite warnings de deprecación. Deben migrarse a
  releases oficiales con runtime Node 24 sin debilitar los gates.

## Trabajo técnico preparado sin merge

### PR #99 — 043A errores de negocio seguros

- Rama: `fix/043a-errores-negocio-seguros`.
- Normaliza fallbacks de PostgreSQL sin exponer texto técnico generado por DB.
- Conserva mensajes específicos de constraints conocidas.
- Conserva mensajes `P0001` controlados por RPC.
- Conserva status HTTP 400/403/404/409.
- `ConfiguracionController` mantiene `{ error, code }` con mensaje normalizado.
- CI #658: API 206/206, DB 207/207, frontend 374/374, Quality Gate PASS.
- No mergear sin autorización explícita.

## E2E

- Smoke Playwright no autenticado disponible.
- E2E autenticado completo sigue pendiente de staging seguro.
- No usar producción para E2E destructivo.
- Plan canónico: `docs/testing/e2e-staging-plan.md` (`E2E-01` → `E2E-06`).

## Observabilidad

- `/health`: liveness.
- `/health/ready`: readiness real contra PostgreSQL.
- Sigue pendiente logging estructurado, correlación, métricas y alertas de
  aplicación. Ver `docs/observabilidad.md`.

## Deuda técnica real pendiente

El registro canónico es `docs/technical-debt.md`. Después de 042 quedan:

1. cerrar el mapeo seguro/amigable de errores de negocio (PR #99 preparado,
   aún sin merge);
2. migrar las GitHub Actions oficiales que todavía apuntan a Node.js 20;
3. montar y ejecutar E2E autenticado en staging aislado;
4. mejorar observabilidad más allá de health/readiness;
5. completar prueba de carga del backend (issue #85).

Ya **no** deben listarse como deuda pendiente:

- selector global multiinstitución;
- divergencia `academico.*` / aliases internos `configuracion.*`;
- acceso directo de negocio a Supabase;
- análisis C# real de Sonar;
- cobertura sin guardrail;
- hallazgos `High` históricos de Sonar;
- pagos/cobranza;
- grados/jornadas multiinstitución.

## Próximo bloque técnico recomendado

**043B — GitHub Actions sobre runtime Node 24**:

- actualizar los majors antiguos de las actions oficiales;
- preferir referencias reproducibles;
- conservar el comportamiento y parámetros actuales del workflow;
- ejecutar CI + Sonar completos;
- verificar que desaparece el warning `Node.js 20 is deprecated`;
- no mezclar este hardening con cambios funcionales.

Después: staging E2E como bloque separado de infraestructura.

## Documentación

`docs/AI_CONTEXT.md` es la fuente principal de estado funcional/arquitectónico.
`docs/HANDOFF.md` conserva el checkpoint operativo actual.
`docs/technical-debt.md` conserva exclusivamente deuda real abierta + historial
resumido de cierres.

`README.md` todavía contiene una descripción histórica que llega a migración 018;
debe sincronizarse en un cambio documental separado para no confundirlo con el
estado canónico de este archivo.

## Git y operación

- Trabajar siempre en rama; no escribir directamente a `main`.
- No force push.
- No commitear secretos.
- No ejecutar pruebas destructivas contra producción.
- No aplicar migraciones ni cambios de datos reales sin autorización explícita.
- Las reglas operativas completas están en `AGENTS.md`.

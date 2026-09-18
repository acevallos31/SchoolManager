# SchoolManager — AI Context

## Estado general

- Proyecto web de gestión escolar en Angular + .NET + PostgreSQL/Supabase.
- Arquitectura objetivo y vigente: Angular → API .NET → PostgreSQL/Supabase/RPC.
- Supabase directo en frontend queda reservado a autenticación (`auth.ts`).
- Deuda #10 de accesos directos de negocio a Supabase: **RESUELTA y mergeada**
  en Bloque 030 / PR #53.
- Bloque 042 de RBAC dinámico institucional y Superadministrador: **RESUELTO y
  mergeado** en PR #97 (`20bea4f9dd346836205620dce2add6f60d17b372`).
- 043A de normalización final de errores de negocio: **RESUELTO y mergeado** en
  PR #99 (`d622a1f50f3e78b00a9e83f2ae3d80c574e80a5a`).
- 043B de GitHub Actions sobre runtime Node 24: **RESUELTO y mergeado** en PR
  #100 (`e46c01343f3ac9ace3fbce29ca8ba4237ffc156f`).
- Producción está en modo **monoinstitución** (`multiples_instituciones=false`)
  con una institución activa según la última verificación read-only; la
  implementación 042 soporta también modo multiinstitución.
- En `main` las migraciones funcionales llegaron hasta 044. La rama 048 agrega la migración 045; está validada en CI pero NO aplicada en producción.

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
separado. Los cambios 043A/043B no requirieron migraciones ni escrituras de datos.

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

### Identidad e invitaciones — 046/048

046A–046F implementaron preparación de usuario, invitación y entrega por correo. El PR #119 / Bloque 048 completa aceptación y aprobación explícita:

1. nunca se vincula por coincidencia de correo;
2. el `auth_user_id` solicitado se deriva del JWT `sub` en backend;
3. el token de invitación se transporta en fragmento y se conserva temporalmente en sessionStorage durante OAuth;
4. aceptar una invitación crea una solicitud pendiente, no concede acceso por sí sola;
5. aprobar exige `identidad.usuarios.editar` en la institución y reutiliza `vincular_identidad_usuario`;
6. rechazar no modifica `auth_user_id`;
7. las operaciones administrativas quedan auditadas;
8. Angular nunca selecciona ni envía un `auth_user_id` externo.

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

Las migraciones integradas en `main` llegaron hasta 044. La rama 048 agrega 045 y el runner/validation la aplica en PostgreSQL efímero durante CI. La 045 no debe ejecutarse en producción sin autorización explícita.

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
- `040`–`044`: alta/invitaciones de acceso, hardening, emisión/entrega y soporte de identidad.
- `045` (PR #119, aún no productiva): solicitud y operación autorizada de vinculación de identidad.

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

## Manejo de errores / Bloque 043A

`ApiControllerBase` es el punto común de traducción de errores PostgreSQL/RPC
hacia respuestas de aplicación:

- constraints conocidas conservan mensajes de negocio específicos;
- errores desconocidos ya no exponen `PostgresException.MessageText` crudo;
- fallback por familia/SQLSTATE produce mensajes seguros;
- `P0001` conserva mensajes de negocio controlados por las RPC;
- se preservan los status HTTP 400/403/404/409;
- `ConfiguracionController` mantiene el contrato `{ error, code }` usando el
  normalizador compartido.

PR #99 cerró esta deuda con pruebas de integración y CI verde.

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
- PR #99 / 043A pasó API 206/206, DB 207/207, frontend 374/374 y Sonar Quality
  Gate antes del merge.
- PR #100 / 043B migró y fijó por SHA las actions oficiales a releases con
  runtime Node 24: `checkout` v7.0.1, `setup-dotnet` v6.0.0, `setup-node` v7.0.0,
  `upload-artifact` v7.0.1 y `download-artifact` v8.0.1.
- El CI de 043B pasó completo, incluidos upload/download de coberturas y Sonar
  Quality Gate; desapareció el warning específico `Node.js 20 is deprecated`.
- `download-artifact` v8.0.1 puede imprimir `DEP0005 Buffer() is deprecated`;
  es un warning upstream diferente, no el warning de runtime Node 20, y no rompe
  el workflow.

## E2E

- E2E autenticado completo en staging local/efímero quedó cerrado por 044A–044E.
- Usa Supabase local, API .NET y Angular staging con identidades/dataset sintéticos.
- El workflow reusable/nocturno conserva artifacts Playwright ante fallos.
- No usar producción para E2E destructivo.

## Observabilidad

- `/health`: liveness.
- `/health/ready`: readiness real contra PostgreSQL.
- Logging JSON estructurado, RequestId/TraceId, correlación, ProblemDetails seguro y métricas internas quedaron implementados en 045A.
- Render logs permitieron verificar read-only el `UserId` y status de `/api/auth/me` durante el diagnóstico 048.
- Exporters/plataformas externas son decisión operativa futura, no deuda base pendiente.

## Deuda técnica real pendiente

El registro canónico es `docs/technical-debt.md`. Al cierre técnico de 048 permanecen:

1. #14 / issue #85: prueba de carga controlada del backend;
2. #15 / issue #109: auditoría/hardening incremental de RPC históricas `SECURITY DEFINER` expuestas a `authenticated`.

Ya **no** deben listarse como deuda pendiente:

- mapeo seguro de errores PostgreSQL hacia UI (043A / PR #99);
- GitHub Actions sobre runtime Node 20 (043B / PR #100);
- selector global multiinstitución;
- divergencia `academico.*` / aliases internos `configuracion.*`;
- acceso directo de negocio a Supabase;
- análisis C# real de Sonar;
- cobertura sin guardrail;
- hallazgos `High` históricos de Sonar;
- pagos/cobranza;
- grados/jornadas multiinstitución.

## Próximo bloque técnico recomendado

Después de cerrar productivamente 048:

1. abordar #15 / issue #109 por grupos pequeños, sin revocaciones masivas;
2. preparar/cerrar #14 / issue #85 con carga solo en entorno seguro y cualquier medición productiva únicamente con autorización explícita;
3. continuar integración de SchoolManager Móvil contra la API .NET compartida, manteniendo Supabase directo solo para Auth.

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

## 049 — Demo pública aislada (2026-09-18)

- Extiende la decisión 042: la Demo pública vive en **infraestructura separada**
  (frontend/API/Supabase propios), nunca sobre la DB productiva.
- Dentro del entorno Demo, cada visitante obtiene una **institución sandbox por sesión**
  clonada desde una plantilla protegida.
- 049A cerrada: arquitectura + inventario de clonación.
- 049B en curso: migración `046_demo_sandbox_sesiones.sql` agrega
  `instituciones.tipo` (`normal|demo_template|demo_sandbox`) y `demo_sessions`,
  con RLS/revokes e invariantes de sesión.
- Identidad prevista para la Demo pública: Supabase Anonymous Sign-In únicamente en el
  proyecto Demo, un Auth UID por navegador, con claim `is_anonymous`,
  CAPTCHA/Turnstile y rate limiting antes de publicar.
- No se comparten Personas/Usuarios entre sandboxes. RNE, identificación global y
  número de recibo se regeneran/nullifican según corresponda durante el clonado.
- `DemoModeEnabled` deberá quedar apagado por defecto y en producción.
- PR de trabajo: #121 (Draft). No aplicar 046 ni hacer merge hasta completar validación.

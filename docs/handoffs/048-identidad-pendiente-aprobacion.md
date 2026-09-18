# HANDOFF — Bloque 048: identidad pendiente y aprobación explícita

## Fecha
2026-09-18 (UTC-6).

## Estado
- Rama: `chore/048-night-auth-debt-close`.
- PR: **#119** — `feat(identity): 048 aprobación de identidades pendientes y hardening auth`.
- Estado PR: abierto, mergeable, **NO mergeado**.
- Último código validado antes de este commit documental: `764c43d2286677c887218a31bcdc271f3ca24f6f`.
- Producción: sin cambios de DB ni datos durante 048.

## Phase 1 — autenticación Demo cerrada
El mensaje “identidad externa no vinculada” no correspondía al estado real del usuario Demo.

Evidencia de producción obtenida read-only en Render:
- `GET /api/auth/me` → 200;
- `Authenticated=true`;
- `UserId=a6a3d715-2418-4fb5-b95c-072e0ce216cf`;
- coincide con `auth.users.id` y `public.usuarios.auth_user_id`.

La causa raíz estaba en frontend/session-state: `asegurarUsuarioInicial()` podía reutilizar una inicialización fallida y conservar el mensaje anterior. El fix permite reintento y limpia el estado obsoleto después de un perfil válido.

También se separaron semánticamente:
- identidad no vinculada;
- usuario inactivo;
- perfil de Persona incompleto;
- perfil sin permisos aplicables.

## Phase 2 — flujo explícito de identidad pendiente
La solución evita por diseño el auto-link por correo.

### Aceptación
1. La institución prepara y envía una invitación.
2. El token viaja en fragmento `#token=` para no enviarlo en query al servidor.
3. El usuario demuestra control de la invitación y se autentica con Supabase.
4. El backend deriva `auth_user_id` exclusivamente del claim JWT `sub`.
5. La identidad queda como solicitud pendiente de aprobación; todavía no obtiene acceso interno por esa vinculación.

### Aprobación/rechazo
Desde **Configuración → Seguridad y acceso**:
- se distingue Cuenta Activa/Inactiva de Identidad Vinculada/Pendiente/Pendiente de aprobación;
- un operador autorizado puede aprobar o rechazar;
- Angular envía `usuarioId`, `institucionId`, `invitacionId` y operación, **nunca `auth_user_id`**;
- la RPC valida institución, usuario, actor y permiso `identidad.usuarios.editar`;
- aprobar reutiliza `public.vincular_identidad_usuario(...)` de 027;
- rechazar no modifica `auth_user_id`;
- ambas operaciones quedan auditadas.

## Migración 045
Archivo: `database/migrations/045_operacion_vinculacion_identidad_autorizada.sql`.

Incluye:
- metadatos de identidad solicitada en `invitaciones_acceso`;
- `rpc_solicitar_vinculacion_invitacion`;
- `rpc_operar_vinculacion_identidad`;
- grants mínimos: sin ejecución directa para `anon`/`authenticated`, uso backend/service role;
- validation y rollback;
- pruebas positivas y negativas de aislamiento/autorización.

**Estado productivo: NO APLICADA.**

La 045 es requisito de esquema para el código 048. Secuencia operativa:
1. CI/Quality Gate verde;
2. autorización explícita del mantenedor;
3. aplicar 045 en producción y ejecutar validation read-only;
4. solo después fusionar PR #119;
5. verificar despliegue y prueba funcional real.

## Calidad
Run #757 sobre `764c43d`:
- compilación/backend: PASS;
- tests de identidad/autorización: PASS;
- DB integration + migration 045: PASS;
- frontend build/staging/Vercel: PASS;
- frontend tests + coverage gate: PASS;
- Sonar Quality Gate: **PASS**;
- New Code coverage: **80.2%**;
- Reliability: A;
- Security: A;
- Maintainability: A;
- duplicated lines: 0.0%;
- hotspots reviewed: 100%.

## Deuda técnica
No se declara cerrada deuda ajena al alcance:
- #14 / issue #85 — carga controlada backend: ABIERTA.
- #15 / issue #109 — inventario/hardening histórico SECURITY DEFINER: ABIERTA.

## Reglas para continuar
- No hacer merge antes de aplicar/validar 045 en producción.
- No auto-link por correo.
- No exponer `auth_user_id` al frontend para selección administrativa.
- No modificar OAuth Google/Microsoft.
- No usar service_role en navegador.
- No ejecutar DDL productivo sin autorización explícita.

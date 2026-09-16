# Handoff — 046B: hardening de invitaciones / migración 041

## Fecha

2026-09-16 (UTC-6).

## Base

- `main`: `b5dea649ea78f0b86b6eb6662a5bb4d49266feda` (PR #108 / 046A).
- Rama: `fix/046b-hardening-invitaciones-041`.
- La migración 040 ya fue aplicada manualmente y validada en el proyecto Supabase de SchoolManager.
- Validación post-040 en producción: `040` registrada, `invitaciones_acceso` existente con RLS, RPC existente, `usuarios.auth_user_id` nullable y cero invitaciones creadas por la migración.
- Conteos de negocio después de aplicar 040 permanecieron en 7 personas, 3 usuarios y 4 asignaciones de rol; la prueba funcional de la RPC se ejecutó dentro de una transacción con rollback y no dejó datos de prueba.

## Hallazgos posteriores a 040

Supabase Security/Performance Advisor confirmó dos hallazgos directamente relacionados con la nueva superficie de 040:

1. `public.rpc_preparar_invitacion_usuario(...)` es `SECURITY DEFINER` y estaba ejecutable directamente por `authenticated` a través de Data API/PostgREST.
2. Las FK `persona_id`, `usuario_id`, `rol_id` y `solicitada_por` de `public.invitaciones_acceso` no tenían índices dedicados de soporte.

El aviso `RLS enabled no policy` sobre `invitaciones_acceso` es intencional: `anon` y `authenticated` no tienen privilegios directos sobre la tabla. No se debe crear una policy permisiva solo para silenciar el linter.

## Decisión de arquitectura

La aplicación mantiene el contrato Angular → API .NET → PostgreSQL para lógica de negocio. `SeguridadAccesoController` invoca `rpc_preparar_invitacion_usuario` desde la conexión del backend, dentro de una transacción que fija `request.jwt.claim.sub` con el `sub` del JWT autenticado.

Por tanto, la RPC de preparación de invitaciones no necesita exposición directa a `authenticated` por PostgREST. El hardening correcto es retirar `EXECUTE` a `authenticated`, conservar las validaciones internas RBAC de la RPC y verificar que la API .NET siga funcionando.

No se hará una revocación masiva sobre las RPC históricas. La auditoría transversal de funciones `SECURITY DEFINER` quedó registrada como deuda técnica independiente en el issue #109.

## Migración 041

`database/migrations/041_hardening_invitaciones_acceso.sql`:

- exige 040 como prerequisito;
- revoca `EXECUTE` de `rpc_preparar_invitacion_usuario(...)` a `authenticated`;
- conserva el acceso del backend/roles administrativos de base y `service_role`;
- crea índices dedicados:
  - `ix_invitaciones_acceso_persona_id`;
  - `ix_invitaciones_acceso_usuario_id`;
  - `ix_invitaciones_acceso_rol_id`;
  - `ix_invitaciones_acceso_solicitada_por` (parcial para valores no nulos);
- registra `041` en `schema_migrations`.

Se incluye rollback que restaura el contrato de permisos de 040 y elimina únicamente los cuatro índices de 041.

## Validación automatizada

`041_hardening_invitaciones_acceso.validation.sql` debe fallar si:

- 041 no está registrada;
- `authenticated` conserva `EXECUTE` sobre la RPC;
- `service_role` pierde `EXECUTE`;
- falta cualquiera de los cuatro índices;
- RLS queda deshabilitado sobre `invitaciones_acceso`.

Las pruebas de `InvitacionesAccesoTests` separan desde 046B dos contratos:

- **vía backend:** conexión del servidor + `request.jwt.claim.sub`; debe conservar idempotencia, autorización y serialización concurrente;
- **vía Data API directa:** `SET ROLE authenticated`; debe recibir `42501` al intentar ejecutar la RPC.

La prueba API existente `Administrador_prepara_invitacion_idempotente_por_API` es la regresión de extremo a extremo que demuestra que retirar el grant directo no rompe el endpoint .NET.

## Alcance deliberadamente excluido

- No modificar OAuth Google/Microsoft.
- No cambiar vinculación explícita de identidades.
- No crear `auth.users`.
- No enviar correos todavía.
- No implementar aceptación/aprobación de invitaciones todavía.
- No sanear en masa las demás RPC `SECURITY DEFINER`.
- No aplicar 041 a Supabase antes de merge y autorización explícita de producción.

## Deuda transversal

Issue #109 — `tech-debt: auditar superficie SECURITY DEFINER expuesta a authenticated`.

Se resolverá por grupos pequeños después de terminar el flujo funcional de invitaciones, verificando consumidores reales antes de cambiar grants, `SECURITY DEFINER`/`SECURITY INVOKER` o ubicación de esquema.

## Criterio de cierre de 046B

1. migración/validation/rollback 041 versionados;
2. pruebas DB positivas por vía backend y negativa por `authenticated` directo;
3. prueba API de invitación permanece verde;
4. CI API/DB y Sonar verdes;
5. PR fusionado a `main`;
6. aplicar 041 manualmente a Supabase solo con autorización explícita;
7. ejecutar Security/Performance Advisor post-041 y comprobar que desaparezcan los hallazgos específicos de la nueva RPC/FK sin introducir regresiones.

## Siguiente bloque funcional

Después de cerrar 046B: continuar el flujo de invitaciones con envío → aceptación → aprobación, manteniendo separada la autenticación externa (Google/Microsoft/local) de la autorización y roles internos de SchoolManager.

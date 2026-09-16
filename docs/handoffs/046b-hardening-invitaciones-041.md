# Handoff — 046B: hardening de invitaciones / migración 041

## Fecha

2026-09-16 (UTC-6).

## Base

- `main`: `83ea466fbf52958682fee513b9e83fd4dc66858e` (PR #110 / 046B).
- La migración 040 fue aplicada y validada previamente en Supabase.
- La migración 041 fue aplicada manualmente en Supabase el 2026-09-16 y validada posteriormente.
- Configuración productiva conocida: `multiples_instituciones = false`; el código conserva soporte para modo single y multiinstitución.

## Hallazgos posteriores a 040

Supabase Security/Performance Advisor confirmó dos hallazgos directamente relacionados con la nueva superficie de 040:

1. `public.rpc_preparar_invitacion_usuario(...)` era `SECURITY DEFINER` y estaba ejecutable directamente por `authenticated` a través de Data API/PostgREST.
2. Las FK `persona_id`, `usuario_id`, `rol_id` y `solicitada_por` de `public.invitaciones_acceso` no tenían índices dedicados de soporte.

El aviso `RLS enabled no policy` sobre `invitaciones_acceso` es intencional: `anon` y `authenticated` no tienen privilegios directos sobre la tabla. No se debe crear una policy permisiva solo para silenciar el linter.

## Decisión de arquitectura

La aplicación mantiene el contrato Angular → API .NET → PostgreSQL para lógica de negocio. `SeguridadAccesoController` invoca `rpc_preparar_invitacion_usuario` desde la conexión del backend, dentro de una transacción que fija `request.jwt.claim.sub` con el `sub` del JWT autenticado.

Por tanto, la RPC de preparación de invitaciones no necesita exposición directa a `authenticated` por PostgREST. El hardening aplicado retira `EXECUTE` a `authenticated`, conserva las validaciones internas RBAC de la RPC y mantiene disponible la vía backend.

No se hizo una revocación masiva sobre las RPC históricas. La auditoría transversal de funciones `SECURITY DEFINER` permanece registrada como deuda técnica independiente en el issue #109.

## Migración 041

`database/migrations/041_hardening_invitaciones_acceso.sql`:

- exige 040 como prerequisito;
- revoca `EXECUTE` de `rpc_preparar_invitacion_usuario(...)` a `authenticated`;
- conserva acceso para la vía backend y `service_role`;
- crea índices dedicados:
  - `ix_invitaciones_acceso_persona_id`;
  - `ix_invitaciones_acceso_usuario_id`;
  - `ix_invitaciones_acceso_rol_id`;
  - `ix_invitaciones_acceso_solicitada_por`;
- registra `041` en `schema_migrations`.

Se conserva rollback que restaura el contrato de permisos de 040 y elimina únicamente los cuatro índices de 041.

## Validación automatizada previa al merge

CI #710 terminó verde:

- API: 234/234;
- DB: 211/211;
- frontend/build/E2E config/rutas Vercel/cobertura: PASS;
- SonarCloud Quality Gate: PASS;
- Vercel preview del HEAD: deployment completed.

## Validación posterior en Supabase

Supabase registró la migración `20260916231005_hardening_invitaciones_acceso_041`.

Validación estructural y de permisos:

- `041` registrada en `public.schema_migrations`;
- `authenticated`: sin `EXECUTE` sobre `rpc_preparar_invitacion_usuario(...)`;
- `anon`: sin `EXECUTE`;
- `PUBLIC`: sin `EXECUTE`;
- `service_role`: conserva `EXECUTE`;
- rol SQL del backend (`postgres` en la validación administrativa): conserva `EXECUTE`;
- RLS de `public.invitaciones_acceso`: activo;
- cuatro índices FK presentes;
- archivo `041_hardening_invitaciones_acceso.validation.sql`: sin errores.

Los conteos de negocio permanecieron sin cambios después de aplicar 041:

- personas: 7;
- usuarios: 3;
- asignaciones de rol: 4;
- invitaciones: 0.

La migración no creó ni modificó datos de negocio.

## Advisors posteriores a 041

### Security Advisor

El hallazgo específico de `rpc_preparar_invitacion_usuario(...)` desapareció. Permanecen 93 funciones `SECURITY DEFINER` históricas ejecutables por `authenticated`; este inventario forma parte de la deuda transversal #109 y no se modificó dentro de 046B.

`invitaciones_acceso` continúa apareciendo como `RLS enabled no policy`; es una decisión deliberada porque `anon` y `authenticated` carecen de acceso directo a la tabla.

También permanece el aviso global de Supabase Auth `Leaked Password Protection Disabled`, fuera del alcance de 046B.

### Performance Advisor

Las cuatro FK de `invitaciones_acceso` dejaron de aparecer entre las foreign keys sin índice. Los cuatro índices nuevos aparecen inicialmente como `unused_index`, lo cual es esperado porque la tabla tiene cero filas y todavía no existe tráfico funcional de invitaciones.

## Estado de cierre

046B queda **CERRADO**:

1. migración/validation/rollback 041 versionados;
2. pruebas DB positivas por vía backend y negativa por `authenticated` directo;
3. API 234/234;
4. DB 211/211;
5. Sonar Quality Gate verde;
6. PR #110 fusionado a `main`;
7. 041 aplicada en Supabase;
8. validación posterior sin errores;
9. hallazgos específicos de la nueva RPC y sus cuatro FK corregidos.

## Siguiente bloque funcional

Continuar el flujo de invitaciones con envío → aceptación → aprobación, manteniendo separada la autenticación externa (Google/Microsoft/local) de la autorización y roles internos de SchoolManager.

# 042C — Operaciones seguras de roles

Estado: implementación DB funcional, hardening final en CI
Fecha: 2026-09-13
Rama: `feature/rbac-dinamico-institucional-042`

## Alcance

Este corte implementa creación y clonación de roles institucionales, edición de metadata, reemplazo transaccional de permisos, asignación y desactivación de roles, protección del último administrador institucional y auditoría de cambios sensibles. Ninguna migración de este corte se ejecutará en Supabase durante el desarrollo.

Reglas cerradas: un rol institucional pertenece a una sola institución; no puede recibir permisos de plataforma ni permisos no delegables; el actor solo puede delegar permisos que posee efectivamente en ese mismo ámbito; una plantilla nunca se asigna directamente; el último administrador institucional y el último Superadministrador quedan protegidos contra retiro o desactivación.

## Implementación

El corte se dividió en migraciones pequeñas para mantener revisión, rollback y validación aislados:

- `029_operaciones_roles_institucionales.sql`: bitácora `seguridad_auditoria`, definición por capacidades del administrador institucional y creación de roles institucionales.
- `030_roles_institucionales_clonado_edicion.sql`: clonado de plantillas y edición de nombre/descripción.
- `031_roles_institucionales_invariantes.sql`: trigger universal que impide introducir permisos de plataforma, no delegables o deprecados en roles institucionales; contador del administrador institucional efectivo.
- `032_roles_institucionales_reemplazar_permisos.sql`: reemplazo atómico de permisos con validación de cota de delegación y protección del último administrador.
- `033_roles_institucionales_asignar.sql`: asignación de un rol institucional por `rol_id`, siempre en la institución propietaria.
- `034_roles_institucionales_desactivar.sql`: desactivación de definición y asignaciones con protección del último administrador institucional.
- `035_roles_usuario_proteccion_ultimo_admin.sql`: endurecimiento de `rpc_desactivar_rol_usuario`, incluyendo serialización con advisory locks para el último administrador institucional y el último Superadministrador.

Cada migración tiene archivo de validación y rollback. El rollback de `035` restaura exactamente el contrato previo de `028`: conserva la protección del último Superadministrador y revierte únicamente la guarda institucional incorporada por `035`.

## Modelo de administrador institucional

No se identifica por un nombre fijo de rol. Un usuario cuenta como administrador institucional cuando conserva simultáneamente, dentro de la misma institución, los permisos efectivos `identidad.roles.editar` e `identidad.usuarios.asignar_roles`. Esto permite que la institución cambie nombres y composición de roles sin introducir checks hardcodeados.

Los helpers `usuario_es_admin_institucional`, `contar_admins_institucionales` y la función de trigger no forman parte de la API pública autenticada. Su ejecución directa queda revocada para `public`, `anon` y `authenticated`; las RPC `security definer` los consumen internamente.

## Auditoría

Las operaciones sensibles insertan eventos en `seguridad_auditoria` con actor, institución, acción, entidad y detalle JSON. La tabla tiene RLS habilitado y privilegios explícitamente cerrados para `public`, `anon` y `authenticated`; la lectura administrativa se diseñará en 042D mediante backend/API.

## Pruebas añadidas

`RbacOperacionesInstitucionalesTests` cubre, como mínimo:

- creación, edición, configuración y asignación de un rol institucional;
- rechazo de permisos que el actor no posee;
- rechazo DB de permisos `platform.*` en roles institucionales;
- aislamiento entre instituciones;
- protección del último administrador institucional;
- retiro permitido cuando queda otro administrador activo;
- evidencia de auditoría para cambios sensibles.

El primer cierre funcional del corte pasó completo: 177/177 API, 184/184 DB, 323/323 frontend, 11/11 runtime/Vercel y coverage gate sin regresiones. El hardening de privilegios posterior se vuelve a validar por CI antes de iniciar 042D.

## Pendiente inmediato

1. confirmar CI + SonarCloud después del hardening de privilegios;
2. ampliar casos de clonado de plantillas y desactivación de definiciones si el gate permanece verde;
3. pasar a 042D: backend .NET, DTOs, endpoints/policies y consultas de Configuración;
4. mantener todas las migraciones sin aplicar en Supabase hasta aprobación explícita.

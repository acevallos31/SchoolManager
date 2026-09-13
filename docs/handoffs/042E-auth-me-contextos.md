# 042E — Contrato multiinstitución de `/api/auth/me`

Fecha: 2026-09-13
Rama: `feature/rbac-dinamico-institucional-042`

## Estado de rollout

Las migraciones RBAC 028–035 **todavía no se han aplicado en Supabase/producción**.
Este corte continúa siendo desarrollo y validación sobre la rama/CI. No debe
usarse este documento como autorización para ejecutar migraciones en producción.

## Objetivo

Dejar de depender únicamente de la unión plana `roles`/`permisos` y exponer el
ámbito real de cada asignación para preparar el selector de institución y la
administración de Seguridad y acceso.

## Contrato aditivo

`GET /api/auth/me` conserva por compatibilidad:

- `id`;
- `personaId`;
- `roles`;
- `permisos`.

Y agrega:

- `ambitoGlobal.roles`;
- `ambitoGlobal.permisos`;
- `instituciones[]` con `id`, `nombre`, `nombreCorto`, `roles` y `permisos`.

La unión plana se mantiene durante la transición para no romper guards ni
pantallas existentes. El código nuevo debe preferir el contexto institucional
explícito cuando una operación dependa de una institución concreta.

## Regla de no inferencia

Una institución solo aparece en `instituciones[]` cuando existe una asignación
activa con `usuarios_roles.institucion_id` explícito. Los roles globales legacy
no se expanden artificialmente a todas las instituciones.

Esto evita fabricar membresías y deja visible la deuda de migración de los
usuarios que todavía dependan de roles globales heredados.

## Compatibilidad previa a 028

La consulta implementada para 042E usa columnas ya existentes antes de 028:
`usuarios_roles.institucion_id`, `roles`, `roles_permisos`, `permisos` e
`instituciones`. No depende de `roles.tipo` ni de `permisos.ambito`.

Por eso backend y frontend pueden validar el nuevo contrato sin ejecutar aún la
migración 028 en Supabase. `platform_admin` aparecerá en `ambitoGlobal` solo
después de que 028 exista y el rol sea asignado explícitamente durante el
bootstrap auditado.

## Instituciones inactivas

Una asignación hacia una institución inactiva no se devuelve como contexto y
no aporta roles/permisos a la unión efectiva de `/auth/me`.

## Frontend

`AuthService` incorpora:

- `ambitoGlobal()`;
- `institucionesDisponibles()`;
- `tieneRolEnInstitucion()`;
- `tienePermisoEnInstitucion()`;
- `esSuperadministrador()`.

Los campos contextuales son temporalmente opcionales en TypeScript para permitir
un despliegue coordinado con una versión de backend anterior durante el rollout.
No se usa la unión legacy como sustituto falso de `ambitoGlobal`.

## Siguiente paso

Construir un `ContextoInstitucionService` que seleccione/persista únicamente un
`institucionId` presente en `instituciones[]`, haga que navegación y servicios
usen los permisos de esa institución y permita luego montar:

`Configuración -> Seguridad y acceso -> Roles y permisos`.

Antes de producción aún falta el bootstrap auditado del primer
Superadministrador y el plan de migración de asignaciones legacy globales.

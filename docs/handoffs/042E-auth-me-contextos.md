# 042E/042F — Contrato y selección multiinstitución

Fecha: 2026-09-13
Rama: `feature/rbac-dinamico-institucional-042`

## Estado de rollout

Las migraciones RBAC 028–035 **todavía no se han aplicado en Supabase/producción**.
Este corte continúa siendo desarrollo y validación sobre la rama/CI. No debe
usarse este documento como autorización para ejecutar migraciones en producción.

## 042E — contrato de `/api/auth/me`

Se dejó de depender únicamente de la unión plana `roles`/`permisos` y se expone
el ámbito real de cada asignación para preparar el selector de institución y la
administración de Seguridad y acceso.

### Contrato aditivo

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

### Regla de no inferencia

Una institución solo aparece en `instituciones[]` cuando existe una asignación
activa con `usuarios_roles.institucion_id` explícito. Los roles globales legacy
no se expanden artificialmente a todas las instituciones.

Esto evita fabricar membresías y deja visible la deuda de migración de los
usuarios que todavía dependan de roles globales heredados.

### Compatibilidad previa a 028

La consulta implementada para 042E usa columnas ya existentes antes de 028:
`usuarios_roles.institucion_id`, `roles`, `roles_permisos`, `permisos` e
`instituciones`. No depende de `roles.tipo` ni de `permisos.ambito`.

Por eso backend y frontend pueden validar el nuevo contrato sin ejecutar aún la
migración 028 en Supabase. `platform_admin` aparecerá en `ambitoGlobal` solo
después de que 028 exista y el rol sea asignado explícitamente durante el
bootstrap auditado.

Una asignación hacia una institución inactiva no se devuelve como contexto y
no aporta roles/permisos a la unión efectiva de `/auth/me`.

## 042F — contexto institucional del frontend

Se agregó `ContextoInstitucionService` como única fuente de la institución de
trabajo seleccionada en el cliente.

Reglas implementadas:

- solo acepta un `institucionId` presente en `instituciones[]` del perfil actual;
- una sola institución se selecciona automáticamente;
- con varias instituciones y sin preferencia válida se exige selección explícita;
- la selección persistida guarda `usuarioId + institucionId`, evitando reutilizar
  el contexto de una cuenta anterior;
- si el usuario pierde acceso a la institución o cierra sesión, la selección se
  limpia/reconcilia;
- roles y permisos por institución pueden consultarse sin mezclarlos con
  `platform.*`;
- backend, RPC y RLS siguen siendo la autoridad: la selección del frontend no
  concede acceso por sí misma.

`AuthService` incorpora:

- `ambitoGlobal()`;
- `institucionesDisponibles()`;
- `tieneRolEnInstitucion()`;
- `tienePermisoEnInstitucion()`;
- `esSuperadministrador()`.

Los campos contextuales son temporalmente opcionales en TypeScript para permitir
un despliegue coordinado con una versión de backend anterior durante el rollout.
No se usa la unión legacy como sustituto falso de `ambitoGlobal`.

### AppShell

El AppShell ya refleja el contexto explícito:

- si existe una sola institución, muestra su nombre y usa la selección automática;
- si existen varias, muestra un selector accesible en la barra superior;
- si todavía no hay selección válida, informa al usuario que debe elegir una
  institución;
- cerrar sesión limpia también el contexto institucional persistido.

En este corte **no se ha cambiado todavía el guard de cada módulo para autorizar
por la institución seleccionada**. Durante la transición conserva la unión
compatible de permisos de `/auth/me`, evitando romper a usuarios legacy antes de
migrar sus asignaciones globales.

## Siguiente paso seguro

Implementar la API .NET de `Configuración -> Seguridad y acceso` sobre las RPC
de 029–035 y después conectar esa pantalla al contexto institucional explícito.
La API deberá rechazar cualquier `institucionId` que no forme parte de los
ámbitos autorizados del usuario y nunca confiar solo en la selección del cliente.

Antes de producción todavía faltan dos pasos de rollout:

1. bootstrap explícito y auditado del primer Superadministrador;
2. migración controlada de asignaciones legacy globales hacia roles
   institucionales explícitos.

Solo después de esas validaciones se debe programar la ejecución de 028–035 en
Supabase y las pruebas de humo de producción.

# 046C — Alta de usuarios desde Seguridad y acceso

Estado: **EN IMPLEMENTACIÓN**

## Objetivo

Cerrar el hueco funcional que impedía crear usuarios nuevos desde `Configuración > Seguridad y acceso`, reutilizando la operación segura introducida en 046A y endurecida en 046B.

## Alcance

- Agregar formulario `Nuevo usuario` en `/configuracion/seguridad-acceso`.
- Solicitar nombres, apellidos, correo y rol institucional inicial.
- Invocar únicamente `POST /api/configuracion/seguridad/usuarios/invitaciones/preparar`.
- Normalizar el correo antes de enviarlo.
- Crear o reutilizar Persona, Usuario interno, asignación institucional e invitación pendiente mediante la RPC existente.
- Refrescar el directorio de usuarios después de la operación.
- Mantener idempotencia: si ya existe una invitación abierta compatible, se reutiliza.

## Límites de seguridad

Este bloque **no**:

- crea filas en `auth.users`;
- crea contraseñas;
- vincula `auth_user_id` por coincidencia de correo;
- envía correos todavía;
- aprueba acceso automáticamente;
- modifica Google/Microsoft OAuth.

La autorización real permanece en backend/PostgreSQL. Aunque la UI solo ofrece el formulario cuando el actor puede administrar asignaciones, la RPC sigue exigiendo de forma independiente `identidad.usuarios.crear` y `identidad.usuarios.asignar_roles`, además de la cota de delegación del rol elegido.

## Flujo

1. Administrador selecciona institución.
2. Abre `Seguridad y acceso`.
3. Completa nombre, apellido, correo y rol inicial.
4. Angular llama a la API .NET.
5. La API ejecuta `rpc_preparar_invitacion_usuario` usando la identidad del actor.
6. La invitación queda `pendiente`.
7. El usuario aparece en el directorio con identidad `Pendiente` hasta que un bloque posterior complete verificación/vinculación/aprobación.

## Siguiente subbloque

Conectar el mismo modelo desde `Responsables` para que un encargado existente pueda solicitar acceso al portal usando su Persona actual y el rol institucional derivado de `parent`, sin duplicar identidades.

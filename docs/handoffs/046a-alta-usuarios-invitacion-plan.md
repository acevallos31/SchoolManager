# 046A — Alta interna de usuarios e invitación pendiente

Estado: **EN IMPLEMENTACIÓN**

## Objetivo

Cerrar la base segura para altas de usuarios y responsables sin crear identidades de Supabase Auth desde el frontend.

Este bloque no envía correos todavía y no ejecuta migraciones en producción. Su alcance es preparar el modelo y una operación backend/DB idempotente que permita crear o reutilizar la identidad interna y dejar una invitación en estado pendiente.

## Reglas de negocio

- La autoridad de permisos permanece en backend + PostgreSQL/RPC/RLS.
- `identidad.usuarios.crear` es obligatorio para altas institucionales.
- El alta nunca asigna automáticamente `platform_admin` ni `school_admin`.
- La coincidencia por correo no vincula `auth_user_id` automáticamente.
- Si un correo corresponde a más de una persona/usuario o hay una identidad externa ya vinculada a otro usuario, la operación debe detenerse.
- Un usuario interno pendiente puede existir sin `auth_user_id`.
- La invitación representa intención de acceso, no una identidad autenticada.
- El rol solicitado debe ser institucional, activo, perteneciente a la institución y delegable por el actor.
- Todas las operaciones deben ser idempotentes y auditables.

## Flujo 046A

1. Operador autorizado selecciona institución, datos de persona, correo y rol inicial.
2. La operación busca de forma inequívoca una Persona reutilizable o crea una nueva.
3. Crea/reutiliza `public.usuarios` asociado a esa Persona, inicialmente sin `auth_user_id` cuando todavía no existe identidad.
4. Asigna el rol institucional permitido.
5. Crea o reutiliza una invitación institucional pendiente.
6. Devuelve IDs y estado normalizado.
7. No llama todavía al proveedor de correo ni a Supabase Admin Auth.

## Estados previstos de invitación

- `pendiente`
- `enviada`
- `aceptada`
- `aprobada`
- `rechazada`
- `expirada`
- `revocada`

En 046A solo se crea/actualiza de forma segura el estado `pendiente`; los demás estados quedan reservados para los siguientes subbloques.

## Integración posterior

- 046B: UI Nuevo usuario y edición de usuario.
- 046C: Responsable -> solicitar acceso -> rol `parent`.
- 046D: signup/vinculación/aprobación y `/acceso-pendiente` por estado real.
- 046E: envío/reenvío de correo, expiración, auditoría y E2E.

Relacionado: #107.

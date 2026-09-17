# 046D — Invitación de acceso para responsables

Estado: **EN IMPLEMENTACIÓN**

## Objetivo

Permitir preparar acceso al portal para un Responsable ya existente sin crear otra Persona ni vincular identidades por coincidencia de correo.

## Regla de identidad

La operación recibe `responsable_id` y PostgreSQL obtiene el `persona_id` exacto desde `public.responsables`. El correo solo se usa como destino futuro de la invitación; **nunca** se usa para elegir qué Persona debe recibir el acceso.

## Migración 042

Agrega `public.rpc_preparar_invitacion_responsable(uuid)`:

- exige responsable/persona/institución activos;
- exige correo en la Persona existente;
- exige `identidad.usuarios.crear` y `identidad.usuarios.asignar_roles`;
- reutiliza o crea `public.usuarios` con `auth_user_id = null`;
- reutiliza o crea un rol institucional `parent` derivado de la plantilla global `parent`;
- si debe crear dicho rol, exige `identidad.roles.crear` y respeta las reglas de delegación de permisos;
- asigna el rol institucional `parent`;
- crea/reutiliza invitación abierta con `origen = responsable` y `estado = pendiente`;
- audita rol, usuario, asignación e invitación cuando son creados;
- serializa por responsable y por institución para evitar duplicados concurrentes.

## Seguridad

La RPC es `SECURITY DEFINER` con `search_path` fijado y se revoca `EXECUTE` a `PUBLIC`, `anon` y `authenticated`. Solo `service_role` recibe grant explícito; la API .NET la ejecuta mediante su conexión PostgreSQL y fija la identidad del actor en la transacción.

No crea `auth.users`, no crea contraseñas, no vincula Google/Microsoft/local y no envía correo todavía.

## API

`POST /api/responsables/{responsableId}/invitacion-acceso/preparar`

La API no recibe Persona, correo ni rol del cliente. Esos datos se resuelven en PostgreSQL a partir del Responsable.

## Producción

La migración 042 **NO debe aplicarse a Supabase antes de CI verde, merge y autorización explícita**.

## Siguiente paso

Después de validar DB/API, agregar la acción `Invitar al portal` en la pantalla de Responsables. El envío real del correo y el flujo aceptación → vinculación explícita → aprobación quedan para bloques posteriores.

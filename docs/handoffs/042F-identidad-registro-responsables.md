# 042F — Identidad, registro y acceso de responsables

Estado: **implementación frontend OAuth iniciada; flujo de autoaprovisionamiento institucional definido para la siguiente operación segura**.

## Objetivo

Cerrar el flujo de identidad de SchoolManager alrededor de tres casos reales:

1. permitir elegir explícitamente otra cuenta de Google al iniciar sesión;
2. agregar inicio de sesión con Microsoft mediante el proveedor `azure` de Supabase;
3. preparar el alta segura de padre/encargado para que una persona registrada como responsable de un alumno pueda recibir un perfil de acceso sin duplicar identidades.

## OAuth Google y Microsoft

La pantalla de login ofrece dos proveedores externos con el mismo nivel visual:

- **Google**: `provider = google` y `prompt=select_account`;
- **Microsoft**: `provider = azure`, scope `email` y `prompt=select_account`.

Ambos regresan a `/auth/callback`. El selector explícito evita reutilizar silenciosamente la última cuenta autenticada en el navegador cuando una persona posee varias cuentas.

Los metadatos entregados por el proveedor pueden utilizarse como ayuda de presentación o prellenado, pero **no conceden roles, permisos ni membresías institucionales**. La autorización sigue residiendo en `public.usuarios`, `usuarios_roles`, roles/permisos, RPC y RLS.

## Perfil visual

La identidad visible debe resolverse en este orden:

1. nombre de `public.personas` entregado por `/api/auth/me`;
2. nombre del proveedor OAuth como fallback de transición;
3. correo de la sesión;
4. `Usuario` únicamente como último fallback.

La cabecera mostrará nombre humano + rol visible. El avatar usará foto del proveedor solo si posteriormente se habilita esa preferencia; el fallback oficial son iniciales derivadas del primer nombre y primer apellido.

## Padre o encargado: fuente de verdad

Registrar a alguien como responsable de un alumno **no debe crear automáticamente una identidad OAuth ni una contraseña**.

El flujo objetivo es:

1. crear o reutilizar `public.personas`;
2. crear o reutilizar `public.responsables` para la institución;
3. crear el vínculo `alumno_responsable`;
4. si se desea acceso al portal, crear/reutilizar un `public.usuarios` asociado a la misma persona, inicialmente sin `auth_user_id`;
5. crear/reutilizar un rol institucional derivado de la plantilla global `parent`;
6. asignar ese rol al usuario dentro de la institución;
7. enviar invitación/verificación al correo definido para el encargado;
8. después de verificar el correo e iniciar sesión con Google, Microsoft o el mecanismo de invitación, vincular explícitamente el `auth_user_id` al usuario interno;
9. habilitar el Portal del responsable únicamente después de que identidad, usuario, rol y vínculo con el alumno sean coherentes.

## Verificación por correo

Cuando la institución defina la cuenta de acceso del padre/encargado, el correo debe verificarse antes de vincular una identidad externa o habilitar el portal.

Reglas:

- el correo de contacto de `personas` no implica por sí solo una identidad autenticada;
- no se vincula OAuth automáticamente solo porque el correo coincida;
- una coincidencia inequívoca puede proponerse al operador, pero la operación final debe ser explícita y auditada;
- si el correo está duplicado, ausente o la identidad externa ya está vinculada a otro usuario, el proceso se detiene para revisión;
- nunca se autoasignan `platform_admin` ni `school_admin` por registro, invitación o OAuth.

## Estados de registro previstos

La UX deberá distinguir al menos:

- **perfil incompleto**: identidad autenticada, pero faltan datos obligatorios;
- **pendiente de verificación**: se definió un correo de acceso, pero falta demostrar control del correo;
- **pendiente de vinculación**: identidad válida, pero aún no está enlazada al usuario interno correcto;
- **pendiente de rol/institución**: usuario interno válido sin capacidad aplicable;
- **activo**: identidad vinculada, usuario activo y permisos efectivos disponibles.

`/acceso-pendiente` debe evolucionar para explicar el estado real en lugar de usar un único mensaje genérico.

## Implementado en este subbloque

- servicio frontend `oauth-provider.service.ts`;
- Google con selector de cuenta;
- Microsoft/Azure con scope `email` y selector de cuenta;
- botones visuales Google/Microsoft con logotipos inline;
- estados de carga independientes por proveedor;
- pruebas unitarias del contrato OAuth;
- mensajes de login preparados para el futuro flujo de invitación del responsable.

## Pendiente antes de cerrar completamente 042F

El autoaprovisionamiento de acceso del responsable requiere una operación backend/DB explícita y transaccional. Debe implementarse como operación segura y auditable, sin reutilizar el vínculo por correo de manera implícita.

La implementación recomendada es una RPC/API idempotente que, partiendo de `responsable_id` + institución + correo confirmado por el operador:

- reutilice la persona existente;
- cree/reutilice el usuario interno sin `auth_user_id`;
- garantice un rol institucional derivado de `parent`;
- asigne el rol en la institución;
- registre auditoría;
- devuelva un estado de invitación, pero no escriba una identidad OAuth hasta que el usuario complete la verificación.

Esto se debe probar primero en preview/staging antes de cualquier migración adicional en producción.

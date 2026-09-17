# 046F — Envío seguro de invitaciones por correo

## Objetivo

Cerrar la infraestructura de emisión de invitaciones sin mezclar autenticación con autorización ni exponer el token al navegador.

Este bloque prepara el envío real de correo y su persistencia. La aceptación/claim del enlace se implementará aparte para no bloquear la revisión del prototipo funcional y financiero.

## Flujo implementado

1. Un usuario autorizado prepara una invitación existente (`invitaciones_acceso`).
2. El backend .NET genera 32 bytes aleatorios (256 bits).
3. El token plano existe únicamente en memoria durante la operación de envío.
4. PostgreSQL recibe y persiste solo `SHA-256(token)`.
5. La migración 044 separa claramente:
   - emisión del token;
   - confirmación del correo aceptado por el proveedor;
   - persistencia del error de entrega.
6. El backend entrega el correo mediante `IInvitationEmailSender`.
7. El adaptador actual usa Resend por HTTPS.
8. Solo después de una respuesta exitosa del proveedor se marca la invitación como `enviada`.
9. Si el proveedor falla, la invitación continúa reintentable, se registra el error y se elimina el hash de un token que nunca fue entregado.

## Seguridad

- El token plano no se guarda en PostgreSQL.
- El token no se devuelve en ninguna respuesta HTTP al frontend.
- Los logs no incluyen token, API key ni cuerpo del proveedor.
- `anon` y `authenticated` mantienen bloqueo directo mediante grants + RLS deny-by-default.
- Las RPC nuevas continúan cerradas a `PUBLIC`, `anon` y `authenticated`; el backend usa el canal privilegiado con el contexto del actor para aplicar RBAC institucional.
- `emision_version` evita que una confirmación o error tardío de un intento anterior sobrescriba una reemisión nueva.
- El envío HTTP usa `Idempotency-Key` por invitación/versión para reducir duplicados ante reintentos.

## Configuración

La configuración técnica se encuentra bajo `InvitationEmail`.

Valores no secretos:

- `Provider`: `resend`.
- `FrontendBaseUrl`: FQDN público del frontend, por ejemplo `https://schoolmanager.nocpbx.com`.
- `From`: remitente validado, por ejemplo `SchoolManager <acceso@schoolmanager.nocpbx.com>`.
- `TtlHours`: duración del enlace; valor inicial 24 horas.

Secretos de producción:

- `InvitationEmail__ApiKey`: API key de Resend. Debe existir solamente como secret/env var en Render; no se almacena en repositorio ni DB.

## Dominio y entregabilidad

Antes de activar envío real:

1. Registrar/verificar el dominio o subdominio remitente en Resend.
2. Publicar los registros DNS solicitados por el proveedor (SPF/DKIM y preferiblemente DMARC).
3. Definir `InvitationEmail__From` con una dirección del dominio verificado.
4. Definir `InvitationEmail__ApiKey` en Render.
5. Confirmar que `FrontendBaseUrl` apunte al FQDN público correcto.

En una fase posterior se podrá exponer en `Configuración > Correo e invitaciones` el FQDN, nombre/dirección del remitente, TTL y correo de prueba. La API key seguirá siendo exclusivamente un secreto del despliegue.

## Supabase Auth

No se reemplaza este flujo por `inviteUserByEmail` de Supabase Auth.

SchoolManager mantiene autoridad sobre Persona → Usuario → Institución → Rol → Permisos → Invitación. Supabase Auth sigue resolviendo autenticación (Google, Microsoft, sesión y eventualmente credenciales locales). No se debe vincular identidad automáticamente solo porque el email coincida.

Los emails propios de Supabase Auth (recuperación, confirmación, etc.) pueden usar SMTP de Resend de forma independiente.

## Migración 044

Archivos:

- `database/migrations/044_invitaciones_envio_confirmado.sql`
- `database/migrations/validation/044_invitaciones_envio_confirmado.validation.sql`
- `database/migrations/rollback/044_invitaciones_envio_confirmado.rollback.sql`

La migración añade/versiona el estado de entrega y las RPC internas para emitir, confirmar y registrar error de envío.

**Estado de producción:** 044 todavía no está aplicada en Supabase. Requiere autorización explícita antes de ejecutar DDL en producción.

## Backend

- `Identity/InvitationEmailSender.cs`: contrato + adaptador Resend.
- `Identity/InvitationDeliveryService.cs`: token, hash, persistencia, envío, confirmación/error.
- `Controllers/InvitacionesController.cs`: `POST /api/invitaciones/{invitacionId}/enviar`.

El endpoint nunca devuelve el token.

## Pruebas

Incluye pruebas de:

- Authorization Bearer a Resend.
- `Idempotency-Key`.
- ausencia de llamada HTTP si falta API key.
- errores del proveedor normalizados sin filtrar su respuesta.
- emisión/confirmación/error contra PostgreSQL real.
- rechazo de confirmaciones obsoletas por `emision_version`.
- migraciones activas hasta 044.
- cliente frontend para el endpoint sin exposición del token.

## Límite intencional del prototipo

El envío todavía no se dispara automáticamente desde los botones existentes porque `/invitacion/aceptar` y el claim seguro de identidad aún no están implementados. Mandar enlaces reales antes de tener consumidor sería un flujo incompleto.

Para el primer prototipo, 046F deja la infraestructura lista y segura sin bloquear la validación de lógica académica/financiera. La activación del botón y aceptación se retomarán como 046G.

## Estado de cierre

- PR: #114 — `feat(identity): 046F envío seguro de invitaciones por correo`.
- Rama: `feature/046f-envio-correo-invitaciones`.
- CI final: run #729 completamente verde.
- SonarCloud Quality Gate: PASS.
- Merge: squash completado en `main`.
- Merge SHA: `4cd39743ee5cd1356630f804d73fcefbc8b0c163`.
- Producción: despliegue de código sujeto al pipeline normal; migración 044 no aplicada todavía a Supabase.

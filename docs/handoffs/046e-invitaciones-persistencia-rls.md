# 046E — Persistencia segura y RLS de invitaciones

## Objetivo

Endurecer la persistencia de `invitaciones_acceso` antes de implementar correo, aceptación y aprobación. El secreto de invitación no debe persistirse en claro ni exponerse al navegador.

## Decisiones

- El token plano será generado por el backend con entropía criptográfica.
- PostgreSQL solo recibe y persiste `SHA-256(token)` en hexadecimal minúscula.
- El token plano no se registra en DB, auditoría ni respuesta frontend.
- Reemitir una invitación reemplaza `token_hash`; el token anterior queda invalidado inmediatamente.
- TTL admitido por DB: mínimo 15 minutos, máximo 7 días.
- La emisión solo aplica a invitaciones `pendiente` o `enviada`.
- La RPC de emisión conserva autoridad RBAC institucional estricta.

## Migración 043

`database/migrations/043_invitaciones_persistencia_tokens_rls.sql`

Agrega a `invitaciones_acceso`:

- `token_hash`
- `token_emitido_at`
- `expira_at`
- `enviado_at`
- `aceptado_at`
- `aprobado_at`
- `rechazado_at`
- `revocado_at`
- `intentos_envio`
- `ultimo_error_envio`

Índices:

- `ux_invitaciones_acceso_token_hash` parcial y único.
- `ix_invitaciones_acceso_estado_expira` para expiración/limpieza.

Restricciones:

- hash SHA-256 hexadecimal minúscula (64 caracteres);
- contador de intentos no negativo;
- expiración posterior a la emisión.

## RLS / Data API

La tabla ya estaba cerrada mediante `REVOKE`, pero 043 añade defensa en profundidad:

- RLS permanece habilitado;
- policy restrictiva `invitaciones_acceso_denegar_data_api` para `anon` y `authenticated` con `USING (false)` / `WITH CHECK (false)`;
- se reiteran los `REVOKE` a `PUBLIC`, `anon` y `authenticated`;
- `service_role` conserva `SELECT/INSERT/UPDATE`;
- el backend PostgreSQL sigue siendo la vía de negocio.

El objetivo es que un `GRANT` accidental futuro no abra filas por Data API mientras la policy restrictiva exista.

## RPC interna

`public.rpc_emitir_invitacion_acceso(uuid,text,timestamptz)`:

- `SECURITY DEFINER`;
- `search_path = pg_catalog, public, pg_temp`;
- no ejecutable por `PUBLIC`, `anon` ni `authenticated`;
- ejecutable por `service_role` y por la conexión backend privilegiada;
- exige actor interno válido y permisos `identidad.usuarios.crear` + `identidad.usuarios.asignar_roles`;
- persiste solo hash/TTL/timestamps;
- incrementa `intentos_envio`;
- audita la emisión sin registrar el token.

## Pruebas

`InvitacionesPersistenciaTests` cubre:

1. persistencia del hash y timestamps;
2. reemisión invalida el hash anterior;
3. contador de intentos;
4. bloqueo directo de la RPC para `authenticated`;
5. hash inválido rechazado;
6. RLS oculta la invitación incluso si un test concede temporalmente `SELECT` a `authenticated`.

Además existen validation/rollback versionados para 043 y el catálogo de migraciones se amplía a 001→043.

## Fuera de alcance de 046E

- proveedor SMTP/email;
- generación del token en endpoint definitivo;
- aceptación del token;
- vínculo explícito con `auth.users`;
- aprobación/rechazo por usuario autorizado;
- expiración automática programada.

Esas piezas deben montarse sobre esta persistencia sin devolver el token plano al frontend.

## Producción

La migración 043 **no debe aplicarse a Supabase antes de merge, CI verde y autorización explícita de producción**.

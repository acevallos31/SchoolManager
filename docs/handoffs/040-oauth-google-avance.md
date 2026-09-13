# SchoolManager — Avance OAuth con Google

**Fecha:** 12 de septiembre de 2026
**Estado:** configuración externa completada; implementación frontend pendiente.

## Arquitectura

Google solamente verifica la identidad. Supabase Auth administra el inicio de sesión y emite el JWT. Angular envía ese JWT al backend .NET mediante `Authorization: Bearer <token>`.

El backend valida firma, issuer, audience, expiración y `sub`. Luego relaciona `auth.users.id` con `public.usuarios.auth_user_id` y carga los roles y permisos internos desde `usuarios_roles`, `roles`, `roles_permisos` y `permisos`.

Los roles y permisos siguen siendo propiedad de SchoolManager. No se tomarán de Google, Microsoft, grupos, claims personalizados ni del frontend.

## Configuración completada

### Google Cloud

- Proyecto: `SchoolManager`
- Project ID: `schoolmanager-508407`
- Cliente OAuth: aplicación web
- Origen autorizado: `https://schoolmanager.nocpbx.com`
- Callback OAuth autorizado: `https://pzhcpdznjoyukbhhodjz.supabase.co/auth/v1/callback`
- El JSON descargado se considera secreto y no debe versionarse.

### Supabase

- Proyecto: `pzhcpdznjoyukbhhodjz`
- Proveedor Google habilitado.
- Client ID y Client Secret configurados en Google.
- Nonce checks habilitados.
- Usuarios sin correo deshabilitados.
- Site URL: `https://schoolmanager.nocpbx.com`
- Redirect URLs:
  - `https://schoolmanager.nocpbx.com/auth/callback`
  - `https://school-manager-j1x4sgdu2-acevallos31s-projects.vercel.app/auth/callback`

### Vercel y Render

El preview utilizado es `https://school-manager-j1x4sgdu2-acevallos31s-projects.vercel.app/`.

Vercel todavía muestra únicamente el login tradicional. Render no necesita Client ID ni Client Secret de Google; solamente valida JWT de Supabase y permite los orígenes autorizados mediante CORS.

## Sesión

La sesión seguirá siendo administrada por Supabase Auth. El access token se renueva mediante el refresh token. El backend no mantendrá una sesión paralela. La sesión se destruye al cerrar sesión, al deshabilitarse el usuario, al revocarse el refresh token o al expirar por seguridad.

## Pendientes

1. Agregar el botón **Continuar con Google** en Angular.
2. Invocar `supabase.auth.signInWithOAuth({ provider: 'google' })`.
3. Crear/procesar la ruta `/auth/callback`.
4. Confirmar el fallback SPA de Vercel para `/auth/callback`.
5. Mantener listener, renovación y sincronización server-side.
6. Verificar que `jwt.interceptor.ts` envíe JWT únicamente a la API.
7. Validar permisos internos mediante `/api/auth/me`.
8. Ejecutar pruebas en preview y producción.

## Restricciones

- Nunca colocar Client Secret en Angular, Vercel, móvil o GitHub.
- No crear login OAuth paralelo en .NET.
- No generar JWT propios.
- No autorizar únicamente por correo.
- No copiar roles desde Google o Microsoft.

## Siguiente paso

Implementar el botón y el flujo Angular en `frontend/schoolmanager-frontend`, conservando `auth.ts` como servicio unificado y manteniendo el backend como autoridad final de roles y permisos.


## Estado posterior de implementación

Se implementó en la rama `docs/oauth-google-avance-040`:

- Botón **Continuar con Google** en la pantalla de login.
- Método `AuthService.loginWithGoogle()` usando `signInWithOAuth({ provider: 'google' })`.
- Redirect dinámico a `${window.location.origin}/auth/callback`, compatible con producción y preview autorizado.
- Ruta Angular `/auth/callback`.
- Componente de callback que espera la restauración de sesión y redirige al dashboard o al portal responsable según los roles internos.
- Se conserva la validación de permisos en backend y la sincronización server-side existente.

Pendiente de validación: ejecutar pruebas frontend, build de producción, CI/Sonar y prueba manual del consentimiento de Google en producción y preview. El Client Secret no fue agregado al repositorio ni al código Angular.


## Resultado de la prueba manual — 12 de septiembre de 2026

CI/CD quedó verde después de corregir el espacio final del handoff y la prueba unitaria del estado `cargandoGoogle`. Sin embargo, la prueba manual del login OAuth continúa con este comportamiento:

1. El usuario pulsa **Continuar con Google**.
2. Google solicita la cuenta y muestra el consentimiento.
3. Google completa la autorización.
4. La aplicación regresa a `/login`.
5. No queda una sesión visible ni se llega al dashboard.

Esto indica que la configuración externa de Google/Supabase permite iniciar el flujo, pero todavía existe un problema en una etapa posterior: callback, restauración de sesión, sincronización server-side, resolución de `/api/auth/me`, cookie/middleware o asociación de la identidad Google con `public.usuarios.auth_user_id`.

### Estado

- Compilación: verde.
- Tests frontend: verdes.
- SonarCloud/Quality Gate: verde.
- Vercel preview: disponible después del último commit.
- Prueba manual OAuth: pendiente; vuelve a `/login`.
- PR: abierto; no fusionar hasta encontrar la causa.

### Investigación requerida

La investigación debe revisar el flujo completo con logs y trazas seguras, sin imprimir access tokens, refresh tokens, Client Secret ni datos personales completos. Debe distinguir si:

- Supabase no restaura la sesión en `/auth/callback`.
- `asegurarUsuarioInicial()` falla al consultar `/api/auth/me`.
- El usuario Google no está vinculado a `public.usuarios.auth_user_id`.
- La sincronización `POST /api/auth/session` falla.
- El middleware redirige por cookie ausente o inválida.
- La URL de callback o el fallback de Vercel alteran el flujo.

## Diagnóstico y parche del callback — 12 de septiembre de 2026

Hermes confirmó dos bloqueos del despliegue:

1. Vercel enviaba `/auth/callback` al catch-all 404 y servía una página estática sin JavaScript. El fragmento OAuth no podía ser procesado por Supabase Auth.
2. `/api/auth/session` respondía 500 antes de validar el token. La función importaba el helper desde fuera de `api/`, por lo que no llegaba a emitir la cookie `__Host-schoolmanager-session`.

Parche aplicado en el PR #89:

- Regla explícita `^/auth/callback/?$ -> /index.html` antes del catch-all.
- Validación del token contenida en `api/auth/session.ts`, sin importación entre directorios.
- Pruebas de regresión para la ruta de Vercel y para POST/DELETE de la función de sesión.
- Nuevo paso de CI `npm run test:vercel`.

Estado posterior al parche:

- Código y pruebas agregados en la rama del PR.
- Pendiente: CI, SonarCloud y nuevo preview.
- Pendiente: prueba manual OAuth en el preview.
- Si `/api/auth/me` responde 403, comprobar en Supabase que `auth.users.id` esté vinculado con `public.usuarios.auth_user_id`, que el usuario esté activo y que tenga roles internos.
- No fusionar hasta confirmar callback 200, función de sesión 204, cookie emitida y acceso al dashboard o portal correspondiente.

## Cierre 040 — vinculación explícita de la identidad OAuth

Implementado en la rama `fix/040-vinculacion-identidad-oauth` (PR #91):

- **Migración 027** (`database/migrations/027_vinculacion_identidad_oauth.sql`): crea la RPC `SECURITY DEFINER`:

  ```
  public.vincular_identidad_usuario(p_usuario_id uuid, p_auth_user_id uuid) returns text
  ```

  Nombre real de la función: **`public.vincular_identidad_usuario(p_usuario_id, p_auth_user_id)`**. No existe `vincular_identidad_oauth`; ninguna referencia debe usar o invocar ese nombre.

  Guardas (idempotente, auditable, no vincula por correo, no asigna roles ni permisos):
  - `p_usuario_id` y `p_auth_user_id` obligatorios (`22004`).
  - El destino `public.usuarios.id` debe existir (`P0002`) y estar activo (`P0001`).
  - La identidad `auth_user_id` no puede estar ya vinculada a otro usuario (`23505`).
  - No re-vincula una identidad ya tomada en el mismo usuario (`P0001`) ni reasigna en silencio.
  - `EXECUTE` restringido: `revoke all` a `public/anon/authenticated`; solo `service_role`.
  - Registro en `schema_migrations` (`version = '027'`).

- **Backend:** `IdentidadNoVinculadaException` (incluye `UsuarioInactivoException`), `UsuarioActualService` distingue "identidad no vinculada" vs "usuario inactivo", `AuthController` responde `403` con `codigo: IDENTIDAD_NO_VINCULADA`.

- **Frontend:** `core/services/auth.ts` (`mapearErrorPerfil`, `consumirMensajeSesionInvalida`), `pages/auth-callback` y `pages/login` muestran el mensaje de cuenta no vinculada.

- **Validaciones:** Backend 177/177 · DB 168/168 · Frontend 322/322.

### SQL manual post-merge — MANUAL Y NO EJECUTADO

> Ninguna sentencia fue ejecutada. No se escribió nada en Supabase ni en producción. El operador autorizado las corre a mano después del merge.

1) Aplicar la migración (crea la RPC):

```
database/migrations/027_vinculacion_identidad_oauth.sql
```

2) Vincular la identidad (reemplazar los UUID reales; obtenerlos de `auth.users` y `public.usuarios`):

```sql
select public.vincular_identidad_usuario(
  p_usuario_id    := '<UUID de public.usuarios>',
  p_auth_user_id  := '<UUID de auth.users>'
);
```

Devuelve `vinculado` (escribe) o `ya_vinculado` (par ya coincidente, sin escritura).

3) Verificar:

```sql
select id, activo, persona_id, auth_user_id from public.usuarios;
```

Rollback (si hiciera falta): `database/migrations/rollback/027_vinculacion_identidad_oauth.rollback.sql`.

Verificación read-only tras la migración: `database/migrations/validation/027_vinculacion_identidad_oauth.validation.sql`.

## Incidente posterior al merge — función de sesión Vercel

La prueba real en producción confirmó que Google y Supabase procesan el retorno OAuth: la URL de callback queda en `/auth/callback#`, señal de que el fragmento ya fue consumido. También se verificó directamente en Supabase que:

- La migración 027 está registrada.
- La función `public.vincular_identidad_usuario(uuid, uuid)` existe.
- La identidad Google está vinculada exactamente a un usuario activo.
- El usuario vinculado tiene un rol activo.

El bloqueo restante se aisló en Vercel:

- `DELETE /api/auth/session` devolvía `500 FUNCTION_INVOCATION_FAILED`.
- `POST /api/auth/session` con un token inválido también devolvía el mismo 500.
- Como DELETE no consulta Supabase, el fallo ocurría al cargar o adaptar la función, antes de la validación del token.
- `AuthService.restaurarSesion()` propagaba ese fallo de sincronización, limpiaba la sesión local y terminaba redirigiendo a `/login`.

Corrección propuesta en el PR siguiente:

- Reemplazar las exportaciones HTTP nombradas por el Web Handler predeterminado de Vercel: `export default { fetch(request) { ... } }`.
- Mantener POST, DELETE y respuestas 401/204.
- Responder 405 con cabecera `Allow: POST, DELETE` para métodos no admitidos.
- Ejecutar todas las pruebas a través del mismo handler exportado que cargará Vercel.

Estado del PR #94:

- CI/CD completo: compilación, pruebas backend, base de datos, frontend y pruebas de rutas/funciones Vercel en verde.
- SonarCloud / Quality Gate: verde.
- Vercel Preview: desplegado y marcado **Ready**.
- La prueba HTTP externa del preview no pudo llegar a la función porque la protección SSO de Vercel respondió 302 antes de ejecutar `/api/auth/session`.
- Pendiente antes del merge: validar el endpoint desde una sesión autorizada del preview, o fusionar y comprobar inmediatamente en producción que DELETE devuelve 204, POST inválido devuelve 401 y el login Google crea la sesión.

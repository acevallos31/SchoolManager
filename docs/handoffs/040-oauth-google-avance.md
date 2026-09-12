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

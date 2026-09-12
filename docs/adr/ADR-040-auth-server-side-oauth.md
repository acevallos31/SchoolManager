# ADR-040 — Autenticación server-side/edge y preparación OAuth

## Estado

Propuesta aceptada para implementación incremental.

## Contexto

SchoolManager usa Supabase Auth en el frontend Angular. La sesión actual se restaura en el navegador y `permissionGuard` evita la navegación sin sesión o sin permisos. El backend .NET vuelve a validar el JWT y resuelve el perfil/roles mediante `/api/auth/me`.

El problema es que una petición HTTP directa a una ruta privada como `/dashboard` todavía puede recibir el shell HTML de Angular con `200 OK` antes de que el guard del navegador redirija a `/login`. Aunque el shell no contiene datos privados, la protección ocurre demasiado tarde para un control server-side y para el criterio del Capstone.

Además, se quiere incorporar posteriormente autenticación social con Google y Microsoft (Azure/Microsoft Entra) sin duplicar identidades ni reemplazar el modelo de roles/permisos de SchoolManager.

## Decisión

Se adopta una arquitectura de dos capas de autenticación/autorización:

1. **Vercel Routing Middleware / capa server-side** valida la existencia de una sesión Supabase válida antes de entregar rutas privadas.
2. **Angular `permissionGuard`** se mantiene para navegación y permisos finos de aplicación.
3. **Backend .NET + PostgreSQL/RLS** siguen siendo la autoridad final para autorización y datos.

La autenticación externa solo determina **quién es el usuario**. Los roles y permisos siguen siendo propiedad de SchoolManager y se resuelven mediante `/api/auth/me`.

## Flujo objetivo

```text
/login
  ├─ Correo + contraseña
  ├─ Continuar con Google        (fase OAuth)
  └─ Continuar con Microsoft     (fase OAuth)
          │
          ▼
      Supabase Auth
          │
          ▼
   sesión sincronizada
   con capa server-side
          │
          ▼
Routing Middleware (Vercel)
  ├─ sesión válida   -> continúa a Angular
  └─ sin sesión      -> 302 /login
          │
          ▼
Angular permissionGuard
          │
          ▼
.NET API /auth/me + autorización backend/RLS
```

## Rutas privadas

La protección server-side debe cubrir como mínimo:

- `/dashboard`
- `/alumnos`
- `/matriculas`
- `/configuracion/*`
- `/responsables`
- `/cargos`
- `/pagos`
- `/portal-padre`

Las rutas y recursos públicos deben permanecer accesibles:

- `/login`
- `/auth/*`
- `/404.html`
- `/robots.txt`
- `/sw.js`
- `/manifest.webmanifest`
- assets estáticos

## Sesión y cookies

La meta final es una sesión que pueda ser validada en servidor/edge y navegador. La transición se hará sin romper el login existente:

### Fase 040A — puente server-side

- Mantener Supabase Auth actual en Angular.
- Sincronizar el `access_token` válido con una cookie `HttpOnly`, `Secure`, `SameSite=Lax` gestionada por una función server-side.
- El middleware lee esa cookie y valida el token contra Supabase antes de entregar rutas privadas.
- `logout` elimina tanto la sesión Supabase como la cookie server-side.
- Los eventos de refresh de Supabase vuelven a sincronizar la cookie.

Esta fase permite protección HTTP real sin reescribir de una vez todo el cliente de autenticación.

### Fase 040B — cookie-native / SSR helper

Cuando la base esté estable, evaluar migración completa a `@supabase/ssr` para que la sesión de Supabase sea cookie-native y reducir duplicación entre almacenamiento de navegador y cookie de borde.

No se hará esta segunda transición si aumenta el riesgo del cierre Capstone sin aportar una mejora verificable inmediata.

## OAuth social — siguiente bloque

Google y Microsoft se incorporarán después de estabilizar la protección server-side.

### Google

- Supabase Auth provider: `google`.
- Flujo recomendado: Authorization Code + PKCE.
- Callback permitido bajo `https://schoolmanager.nocpbx.com/auth/callback`.

### Microsoft

- Supabase Auth provider: `azure`.
- Solicitar scope `email`.
- Registrar aplicación en Microsoft Entra ID.
- Callback de Supabase: `https://<project-ref>.supabase.co/auth/v1/callback`.
- El `redirectTo` final de la aplicación será `https://schoolmanager.nocpbx.com/auth/callback`.
- Si en el futuro SchoolManager se limita a una organización concreta, configurar un tenant de Entra específico en lugar de `common`.

## Identidad interna

- `auth.users.id` sigue siendo el identificador de autenticación estable.
- `persona`, roles y permisos no dependen del proveedor OAuth ni del correo como PK.
- Un usuario que use password, Google o Microsoft debe converger en una sola identidad Supabase cuando el linking sea seguro y explícito.
- No crear roles automáticamente a partir del proveedor social.

## Seguridad

- Nunca confiar solo en la presencia de una cookie: el token debe validarse.
- Las respuestas autenticadas deben usar `Cache-Control: private, no-store` para evitar que una CDN comparta contenido entre usuarios.
- No incluir secretos de Google/Microsoft en el repositorio.
- Client IDs y claves publicables pueden configurarse como variables de entorno; client secrets solo en Supabase/Vercel según corresponda.
- El middleware no sustituye la autorización del backend ni RLS.
- Mantener `permissionGuard` como segunda barrera de UX/navegación.

## Criterios de aceptación del bloque 040

1. `curl -I https://schoolmanager.nocpbx.com/dashboard` sin sesión devuelve `302` a `/login`.
2. `/login` sin sesión sigue devolviendo `200`.
3. Una sesión válida permite cargar `/dashboard` directamente y tras refresh.
4. Logout invalida la cookie server-side y vuelve a bloquear rutas privadas.
5. El 404 real, PWA, `robots.txt` y headers de seguridad no regresan.
6. Angular mantiene `permissionGuard` y el backend mantiene `/auth/me`.
7. Tests/CI/Sonar continúan verdes.

## No objetivos de este bloque

- No agregar todavía botones Google/Microsoft.
- No cambiar roles/permisos de negocio.
- No mover autorización desde .NET a Vercel.
- No tocar RLS ni base de datos.
- No convertir Angular a SSR completo.


## Implementación real verificada

La implementación actual usa estos componentes concretos:

- Angular `AuthService` ejecuta `signInWithPassword()`, restaura la sesión persistida y consulta `GET /api/auth/me`.
- `jwt.interceptor.ts` agrega `Authorization: Bearer <access_token>` únicamente a las solicitudes cuyo destino comienza con `environment.apiUrl`.
- `api/auth/session.ts` recibe el Bearer token, lo valida contra Supabase y crea la cookie `__Host-schoolmanager-session` con `HttpOnly`, `Secure`, `SameSite=Lax` y una duración de una hora.
- `middleware.ts` lee esa cookie, vuelve a validar el token contra Supabase y redirige a `/login` cuando falta o es inválido.
- El backend ASP.NET Core valida nuevamente el JWT mediante JWT Bearer; la cookie de Vercel no reemplaza esta validación.
- `permissionGuard` controla la navegación, pero no es la autoridad de seguridad.

Por tanto, el diseño actual conserva dos representaciones de la sesión: el almacenamiento gestionado por Supabase en el navegador y la cookie HttpOnly para el middleware server-side. La migración completa a cookies nativas mediante `@supabase/ssr` sigue siendo una posible fase posterior, no una funcionalidad ya implementada.

Google y Microsoft todavía están documentados como preparación OAuth. No existen aún botones ni flujos sociales implementados en este bloque.

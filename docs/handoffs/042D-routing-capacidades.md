# 042D — Navegación inicial por capacidades

Fecha: 2026-09-13
Rama: `feature/rbac-dinamico-institucional-042`

## Objetivo

Eliminar el acoplamiento directo del login, callback OAuth y AppShell a nombres fijos de rol como `admin` y `padre`, preparando el frontend para roles institucionales dinámicos.

## Implementación

- Se agregó `resolverRutaInicial(usuario)` como punto único de decisión de landing.
- Un usuario con permisos efectivos de aplicación entra al dashboard sin importar el nombre de su rol institucional.
- Se conserva temporalmente la compatibilidad del portal responsable mediante los alias `padre` y `parent`, porque ese portal todavía no dispone de una capacidad funcional propia para decidir la ruta.
- `AuthService.usuarioActual()` expone únicamente el perfil ya cargado en memoria para evitar una segunda consulta a `/auth/me` durante el callback y permitir que los guards tomen la misma decisión de landing.
- `PermissionGuard` valida también que el perfil pueda entrar al AppShell administrativo: un responsable es enviado a su portal y un perfil sin módulo disponible se envía a `/acceso-pendiente`.
- `/acceso-pendiente` queda fuera del AppShell y muestra un mensaje accionable; no destruye silenciosamente una sesión válida.
- Login por contraseña, callback OAuth, acceso directo al AppShell y fallback por falta de permiso usan la misma resolución central.

## Regla de seguridad

Esta resolución solo decide navegación. No concede acceso. `PermissionGuard`, las policies .NET, las RPC y RLS continúan siendo la autoridad para cada operación. La nueva pantalla de acceso pendiente no otorga permisos ni sustituye validaciones del backend.

## Cobertura añadida

Se cubren los casos:

- rol institucional con nombre personalizado y permiso válido -> dashboard;
- responsable -> portal responsable, incluso si intenta entrar al AppShell;
- perfil activo sin capacidad/ruta -> `/acceso-pendiente`, sin logout silencioso;
- permiso concreto denegado -> destino coherente según las capacidades restantes;
- callback OAuth usa el perfil ya cargado y la misma decisión que el login;
- estado autenticado inconsistente sin perfil -> regreso seguro al login.

## Pendiente del bloque 042

La API de administración de roles debe seguir validando el ámbito institucional exacto en backend. El contrato de `/api/auth/me` deberá evolucionar después para exponer contextos/ámbitos explícitos; hasta entonces los permisos siguen llegando como unión efectiva del usuario.

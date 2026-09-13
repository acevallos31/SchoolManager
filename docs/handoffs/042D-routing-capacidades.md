# 042D — Navegación inicial por capacidades

Fecha: 2026-09-13
Rama: `feature/rbac-dinamico-institucional-042`

## Objetivo

Eliminar el acoplamiento directo del login y del callback OAuth a nombres fijos de rol como `admin` y `padre`, preparando el frontend para roles institucionales dinámicos.

## Implementación

- Se agregó `resolverRutaInicial(usuario)` como punto único de decisión de landing.
- Un usuario con permisos efectivos de aplicación entra al dashboard sin importar el nombre de su rol institucional.
- Se conserva temporalmente la compatibilidad del portal responsable mediante los alias `padre` y `parent`, porque ese portal todavía no dispone de una capacidad funcional propia para decidir la ruta.
- Un perfil válido sin una pantalla disponible ya no se cierra automáticamente: se muestra un mensaje accionable para que el administrador revise sus permisos.
- Login por contraseña y callback OAuth usan la misma resolución.

## Regla de seguridad

Esta resolución solo decide navegación. No concede acceso. `PermissionGuard`, las policies .NET, las RPC y RLS continúan siendo la autoridad para cada operación.

## Cobertura añadida

Se cubren los casos:

- rol institucional con nombre personalizado y permiso válido -> dashboard;
- responsable -> portal responsable;
- perfil activo sin capacidad/ruta -> sin navegación automática y sin logout silencioso;
- callback OAuth usa la misma decisión que el login.

## Pendiente del bloque 042

La API de administración de roles debe seguir validando el ámbito institucional exacto en backend. El contrato de `/api/auth/me` deberá evolucionar después para exponer contextos/ámbitos explícitos; hasta entonces los permisos siguen llegando como unión efectiva del usuario.

# Hermes — Google OAuth: vinculación de identidad y RBAC

## Objetivo

Diagnosticar y corregir de forma segura el inicio de sesión con **Continuar con Google**. La hipótesis confirmada por revisión de código es que Supabase crea la identidad en `auth.users`, pero no existe un mecanismo de producción que vincule ese UUID con `public.usuarios.auth_user_id`. Sin ese vínculo, `GET /api/auth/me` devuelve 403, el frontend limpia la sesión recién creada y el callback regresa a `/login`.

Repositorio: `acevallos31/SchoolManager`  
Ruta local: `/opt/projects/SchoolManager`

## Reglas operativas

1. Leer completos y en este orden: `AGENTS.md`, `docs/AI_CONTEXT.md`, `docs/engineering-principles.md`, `README.md` y los archivos específicos del flujo OAuth.
2. Confirmar rama, `git status` y HEAD antes de trabajar.
3. Usar un worktree aislado y una rama descriptiva basada en la rama correcta del PR #89.
4. No trabajar directamente sobre `main`.
5. No modificar ni mezclar `fix/027b-portal-a11y-estados` ni el commit `2518699`.
6. No iniciar otro bloque funcional ni ampliar el alcance.
7. No aplicar migraciones, escrituras, grants ni cambios en Supabase/producción.
8. No imprimir secretos, UUID completos, JWT, correos completos ni cadenas de conexión.
9. Roles y permisos deben seguir siendo autoridad del backend y la base de datos, no del frontend.
10. Se autorizan inspección, implementación, tests, commits, push y apertura/actualización de PR. **No se autoriza merge a main.**
11. Detenerse ante una decisión arquitectónica no resuelta, cambio destructivo, necesidad de escribir datos reales o falta de evidencia suficiente.

## Estado conocido

- PR #50 ya está mergeado.
- El flujo Google se trabaja/prueba en el PR #89; identificar su rama y preview exactos.
- `main` puede no contener aún `signInWithOAuth`.
- La autorización actual sigue esta cadena:
  `GET /api/auth/me` → `UsuarioActualService` → `usuarios.auth_user_id` → `usuarios_roles` → `roles` → permisos.
- No se encontró trigger sobre `auth.users`.
- No se encontró código de producción que escriba `auth_user_id`; solo fixtures de pruebas.
- `AuthCallback` ejecuta `asegurarUsuarioInicial()`.
- Cuando `/api/auth/me` responde 403, `AuthService.restaurarSesionDesdeStorage()` ejecuta la limpieza y `signOut()`.
- El usuario solo recibe un mensaje genérico y vuelve a `/login`.
- La conexión `schoolmanager_auditor` puede carecer de permisos de lectura sobre las tablas necesarias.

## Fase 1 — Evidencia de solo lectura

1. Inspeccionar el PR #89: rama, HEAD, diff, checks, comentarios y URL exacta del preview.
2. Seguir el flujo completo:
   - botón Continuar con Google;
   - `signInWithOAuth`;
   - `redirectTo`;
   - `/auth/callback`;
   - restauración/intercambio de sesión;
   - `asegurarUsuarioInicial()`;
   - `/api/auth/me`;
   - endpoint de cookie;
   - `middleware.ts`;
   - limpieza de sesión y `signOut()`.
3. Confirmar por búsqueda que no existe otro escritor legítimo de `auth_user_id`.
4. Si hay acceso autorizado de solo lectura, ejecutar únicamente:

```sql
select au.id as auth_uid,
       au.email,
       au.created_at,
       u.id as usuario_id,
       u.activo,
       u.persona_id
from auth.users au
left join public.usuarios u
  on u.auth_user_id = au.id
order by au.created_at desc
limit 20;
```

5. Enmascarar correos y UUID en el reporte.
6. Si aparece `permission denied`, no elevar privilegios ni cambiar grants. Documentar el bloqueo.

## Fase 2 — Decisión mínima y segura

Evaluar:

- SQL puntual para vincular una identidad de prueba con un usuario existente.
- RPC `SECURITY DEFINER` segura e idempotente.
- Endpoint administrativo ASP.NET Core para vinculación/aprobación.
- Trigger automático sobre `auth.users`.

La prueba inmediata debe conservar el usuario y sus roles actuales, evitar duplicados y no asignar privilegios nuevos. La solución permanente debe requerir una vinculación verificable o dejar al usuario nuevo pendiente/con rol mínimo. Nunca asignar administrador automáticamente.

Revisar especialmente:

- correo confirmado por Google;
- normalización y unicidad del correo;
- identidad ya vinculada;
- usuario inexistente o inactivo;
- una identidad vinculada a dos usuarios;
- dos identidades vinculadas al mismo usuario;
- usuario sin rol activo;
- idempotencia;
- autorización de quien ejecuta la vinculación;
- RLS y contexto institucional.

## Fase 3 — Implementación

Solo si la causa queda confirmada con evidencia suficiente:

1. Implementar el cambio mínimo en la rama derivada del PR #89.
2. Mantener backend/DB como autoridad de identidad, roles y permisos.
3. Si corresponde una migración, incluir forward, rollback y validación siguiendo la convención del repositorio; probarla solo en Testcontainers o base desechable.
4. No ejecutar la migración contra Supabase.
5. Mejorar el tratamiento del callback para distinguir internamente:
   - sesión OAuth no creada;
   - identidad no vinculada;
   - usuario inactivo;
   - backend temporalmente no disponible;
   - cookie no emitida.
6. Mostrar al usuario un mensaje seguro y accionable, sin filtrar detalles internos.
7. Evitar que un 403 por identidad no vinculada genere bucles de redirección o destruya información útil antes de registrar el motivo seguro.

## Validaciones obligatorias

Ejecutar lo que aplique al diff:

```bash
git diff --check

dotnet build backend/SchoolManager.API/SchoolManager.API.csproj -c Release

dotnet test tests/SchoolManager.API.IntegrationTests/SchoolManager.API.IntegrationTests.csproj -c Release

dotnet test tests/SchoolManager.Database.IntegrationTests/SchoolManager.Database.IntegrationTests.csproj -c Release

cd frontend/schoolmanager-frontend
npm test -- --watch=false
npm run build
```

Añadir o actualizar pruebas para:

- `GET /api/auth/me`;
- identidad vinculada/no vinculada;
- usuario inactivo;
- roles y permisos activos;
- callback OAuth;
- emisión de `__Host-schoolmanager-session`;
- login tradicional sin regresión;
- prevención de bucles;
- persistencia de sesión hasta que el usuario cierre sesión;
- operación idempotente de vinculación.

No ocultar warnings nuevos. Distinguir fallo propio, preexistente y prueba no ejecutada.

## Entrega de Hermes

Dejar:

1. Veredicto en una línea.
2. Entorno exacto auditado.
3. Causa raíz y evidencia.
4. Resultado enmascarado de la consulta o bloqueo de permisos.
5. Solución inmediata y solución permanente.
6. Archivos y migraciones modificados.
7. Pruebas ejecutadas con conteos y resultados exactos.
8. Riesgos y acciones manuales pendientes.
9. Rama, HEAD y commits.
10. PR abierto/actualizado y estado de checks/preview.
11. Confirmación explícita de que no se aplicaron cambios en Supabase ni se hizo merge.

No declarar resuelto hasta comprobar el recorrido Google → callback → sesión → cookie → `/api/auth/me` → roles/permisos. Si la prueba real requiere una vinculación puntual en Supabase, preparar el SQL parametrizado y detenerse para autorización humana antes de ejecutarlo.

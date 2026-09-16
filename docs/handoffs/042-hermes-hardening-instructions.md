# Handoff técnico 042 — hardening previo al rollout

Trabajar únicamente en la rama `feature/rbac-dinamico-institucional-042` del repositorio SchoolManager. El PR asociado es #97. No hacer merge y no ejecutar migraciones en Supabase.

## Objetivo inmediato

Cerrar los dos bloqueos técnicos previos al API de Seguridad y acceso:

1. corregir el refresco zoneless del selector/contexto institucional en AppShell;
2. impedir que un `admin` global legacy obtenga autoridad de administración RBAC sobre cualquier institución por el fallback histórico de permisos globales.

## 1. AppShell zoneless

Archivo: `frontend/schoolmanager-frontend/src/app/layout/app-shell/app-shell.ts`.

Aplicar el cambio mínimo:

- importar e inyectar `ChangeDetectorRef`;
- después de `this.navAbierta = false` en `NavigationEnd`, ejecutar `markForCheck()`;
- después de actualizar `roles` e `instituciones` desde `usuarioActual$`, ejecutar `markForCheck()`;
- después de actualizar `institucionActual` desde `institucionActual$`, ejecutar `markForCheck()`.

No modificar ni relajar las dos pruebas que actualmente fallan en `app-shell.spec.ts`. Esas pruebas deben pasar con el arreglo real.

## 2. Autoridad RBAC institucional estricta

La función histórica `usuario_tiene_permiso(...)` de 007 permite un rol global como fallback para cualquier `p_institucion_id`. Esa semántica se conserva temporalmente para módulos legacy, pero NO debe autorizar las operaciones administrativas del RBAC dinámico.

Implementar un helper interno, con nombre consistente con el proyecto, que evalúe un permiso para administración RBAC institucional con esta semántica:

- `p_institucion_id` obligatorio y correspondiente a una institución activa;
- éxito si el actor autenticado tiene una asignación `usuarios_roles` activa con `institucion_id = p_institucion_id`, rol activo y el permiso solicitado vigente;
- éxito alternativo si el actor tiene una asignación global activa a un rol activo `tipo='plataforma'` y `codigo='platform_admin'`, y dicho rol posee el permiso solicitado;
- un rol global `legacy`, incluido `admin`, nunca satisface por sí solo la autorización institucional estricta;
- función `SECURITY DEFINER`, `search_path` explícito; revocar EXECUTE a `public`, `anon`, `authenticated`; permitir uso interno y `service_role` según el patrón del bloque.

Como 028–035 todavía no han sido aplicadas en producción, preferir corregir las migraciones aún no publicadas en lugar de crear deuda correctiva innecesaria.

Sustituir el chequeo genérico por el estricto en:

- creación de rol institucional;
- clonado de plantilla;
- edición de rol institucional;
- reemplazo de permisos del rol;
- comprobación del techo de delegación de cada permiso;
- asignación de rol institucional;
- desactivación de rol institucional;
- retirada de una asignación institucional.

Revisar también `rpc_asignar_rol_usuario` de 028: si `p_institucion_id` no es nulo, el fallback de un `admin` global legacy no debe permitir asignar un rol dentro de esa institución. Mantener la protección especial de `platform_admin`.

Para asignaciones globales legacy con `institucion_id IS NULL`, conservar solo la compatibilidad necesaria; no convertirlas en Superadministradores ni retirar roles automáticamente.

## 3. Pruebas obligatorias

Extender la suite DB, preferentemente `RbacOperacionesInstitucionalesTests`, para cubrir:

- admin global legacy intenta crear rol institucional: 42501;
- admin global legacy intenta editar/reemplazar permisos/asignar rol institucional: 42501;
- el mismo actor obtiene una asignación institucional explícita con los permisos necesarios: operaciones permitidas;
- actor de institución A no administra B;
- platform_admin global con el permiso correspondiente puede administrar A/B;
- platform_admin sin el permiso objetivo no supera el techo de delegación;
- siguen pasando protección del último administrador institucional y último Superadministrador.

No cambiar la función genérica histórica `usuario_tiene_permiso` para no romper módulos existentes en este bloque.

## 4. Validación

Ejecutar antes del push:

```bash
git diff --check

dotnet build backend/SchoolManager.API/SchoolManager.API.csproj --configuration Release
dotnet test tests/SchoolManager.API.IntegrationTests/SchoolManager.API.IntegrationTests.csproj --configuration Release
dotnet test tests/SchoolManager.Database.IntegrationTests/SchoolManager.Database.IntegrationTests.csproj --configuration Release

cd frontend/schoolmanager-frontend
npm ci --ignore-scripts
npm run test:vercel
npm test -- --watch=false
npm run build
```

No eliminar guardrails de cobertura, autorización, RLS o Sonar.

## 5. Cierre de esta tarea

Si todo pasa:

- commit sugerido: `fix(rbac): exigir ámbito institucional explícito`;
- push a `feature/rbac-dinamico-institucional-042`;
- dejar PR #97 en Draft;
- reportar SHA, resultados exactos de API/DB/frontend/Vercel y CI;
- no aplicar 028–035 en Supabase y no hacer merge.

Después de este cierre se continúa con el API mínimo `Configuración > Seguridad y acceso`.
# Bloque 042 — Preflight previo al rollout RBAC

## Estado

Las migraciones 028–035 permanecen sin aplicar en Supabase. Producción registra la cadena 007–027 y el baseline histórico. El PR #97 sigue en Draft.

El CI previo mantiene backend, API, base de datos y runtime Vercel en verde. El frontend tiene dos pruebas fallidas del AppShell relacionadas con actualización visual del contexto institucional bajo zoneless change detection.

## Gates antes de producción

1. Corregir AppShell sin relajar las pruebas: el estado recibido por RxJS debe marcar el componente para refresco.
2. Endurecer la administración RBAC institucional: un permiso global legacy no debe convertirse en autoridad implícita sobre todas las instituciones.
3. Para operaciones de roles/permisos institucionales, exigir una asignación activa explícita en la institución, salvo la excepción controlada de platform_admin.
4. Mantener la cota de delegación: solo permisos institucionales, vigentes, delegables y poseídos por el actor en el ámbito correcto.
5. Completar la API mínima de Configuración > Seguridad y acceso usando RPC/autorización contextual; no confiar solo en policies .NET globales.
6. Preparar el bootstrap auditado del primer platform_admin. No promover automáticamente ningún admin legacy.
7. Ejecutar la suite completa y exigir CI/Sonar verdes.
8. Solo entonces aplicar la cadena RBAC en Supabase, ejecutar sus validaciones y realizar pruebas reales de autenticación, aislamiento y administración de roles.

## Hallazgos del preflight de Supabase

- Existe una asignación admin global legacy, una admin institucional y una consulta institucional activas.
- El esquema productivo aún no contiene los metadatos nuevos de roles/permisos ni platform_admin.
- schema_migrations tiene RLS deshabilitado; sin embargo, anon y authenticated no poseen privilegios directos de lectura sobre la tabla, mientras service_role conserva el acceso necesario. El hallazgo se mantiene como hardening defensivo y debe resolverse sin bloquear el mecanismo de migraciones.

## Corrección AppShell zoneless

El fallo no debe resolverse cambiando las pruebas. AppShell recibe cambios desde `usuarioActual$`, `institucionActual$` y `NavigationEnd`, pero los almacena en campos ordinarios. Con zoneless change detection esas emisiones externas necesitan marcar el componente.

Cambio mínimo aprobado:

- inyectar `ChangeDetectorRef` en AppShell;
- ejecutar `markForCheck()` después de actualizar `roles`/`instituciones`;
- ejecutar `markForCheck()` después de actualizar `institucionActual`;
- ejecutar `markForCheck()` al cerrar el drawer por `NavigationEnd`;
- conservar las pruebas que verifican institución única, selector multiinstitución y contexto requerido.

## Regla de autoridad institucional

La función genérica histórica de permisos acepta asignaciones globales como fallback para compatibilidad. Esa semántica debe conservarse temporalmente para módulos legacy, pero no debe reutilizarse como autoridad de administración RBAC institucional.

Se implementará un helper interno de autorización RBAC contextual con estas reglas:

- requiere `p_institucion_id` no nulo y una institución activa;
- permite el permiso cuando el actor tiene una asignación activa explícita en esa institución cuyo rol activo contiene el permiso vigente solicitado;
- alternativamente permite a un `platform_admin` global activo si ese rol contiene el mismo permiso solicitado;
- un `admin` global legacy por sí solo nunca satisface esta comprobación;
- el helper es interno: `SECURITY DEFINER`, `search_path` explícito y sin EXECUTE para `public`, `anon` ni `authenticated`.

La comprobación estricta se usará en las operaciones institucionales de las migraciones 029–035: crear rol, clonar plantilla, editar rol, reemplazar permisos, comprobar techo de delegación, asignar rol, desactivar rol y retirar una asignación institucional.

La RPC genérica heredada de asignación también deberá evitar que el fallback global legacy conceda roles dentro de una institución. Las operaciones globales legacy se conservan únicamente como compatibilidad transitoria; `platform_admin` mantiene su protección exclusiva.

## Cobertura obligatoria del hardening

La suite de DB debe demostrar al menos estos escenarios:

- un admin global legacy no puede crear un rol institucional sin una asignación explícita en esa institución;
- un admin global legacy no puede editar, asignar ni reemplazar permisos de un rol institucional usando solo el fallback global;
- un administrador con asignación institucional explícita conserva las operaciones autorizadas;
- un administrador de institución A no puede administrar roles de institución B;
- platform_admin puede operar entre instituciones solo con los permisos correspondientes;
- ningún actor puede delegar un permiso institucional que no posea en su ámbito autorizado;
- siguen protegidos el último administrador institucional y el último Superadministrador.

## API mínima de Seguridad y acceso

La primera superficie administrativa se implementará bajo `api/configuracion/seguridad` y siempre recibirá un contexto institucional explícito. El contrato inicial previsto es:

- `GET /roles?institucionId=...`: roles institucionales activos/inactivos autorizados;
- `GET /plantillas?institucionId=...`: plantillas globales disponibles para clonar;
- `GET /permisos?institucionId=...`: permisos visibles y delegables que el actor puede conceder;
- `GET /asignaciones?institucionId=...`: usuarios y roles activos de la institución;
- `POST /roles`: crear rol institucional;
- `POST /roles/clonar`: clonar una plantilla;
- `PUT /roles/{rolId}`: editar nombre/descripción;
- `PUT /roles/{rolId}/permisos`: reemplazo transaccional de permisos;
- `POST /roles/{rolId}/asignaciones`: asignar rol a un usuario;
- `POST /asignaciones/{usuarioRolId}/desactivar`: retirar asignación con motivo;
- `POST /roles/{rolId}/desactivar`: desactivar rol con motivo.

Las lecturas deben usar RPC o una ejecución PostgreSQL que preserve el contexto del usuario. No se deben hacer SELECT privilegiados directos desde el backend confiando únicamente en una policy .NET, porque la autoridad final debe permanecer en PostgreSQL/RLS/RPC.

La ruta frontend prevista es `Configuración > Seguridad y acceso`, visible por capacidades de identidad y no por nombres fijos de rol. Para una institución, la pantalla mostrará roles, plantillas, permisos y asignaciones. Las superficies de plataforma para Superadministradores se mantendrán separadas.

## Bootstrap del primer Superadministrador

El primer platform_admin será una operación de rollout privilegiada, manual y auditada. Debe ejecutarse únicamente cuando no exista ya un Superadministrador activo, contra un usuario interno activo y después de confirmar explícitamente la cuenta destino.

El bootstrap debe:

- adquirir el mismo bloqueo transaccional usado para proteger Superadministradores;
- rechazar la operación si ya existe un platform_admin activo;
- crear una única asignación global activa al rol protegido;
- registrar la acción en `seguridad_auditoria`;
- ejecutar inmediatamente validaciones de permisos y autenticación;
- no convertir ni retirar automáticamente los roles legacy existentes.

Las altas posteriores de Superadministradores se harán mediante el flujo normal protegido para `platform.superadmins.gestionar`.

## Rollout previsto

1. CI completamente verde y SonarCloud PASS.
2. Snapshot/preflight final de versiones y asignaciones RBAC en Supabase.
3. Aplicar las migraciones RBAC en orden y ejecutar cada validación asociada.
4. Ejecutar el bootstrap confirmado del primer Superadministrador.
5. Iniciar sesión con una cuenta institucional y comprobar aislamiento/roles.
6. Iniciar sesión con el Superadministrador y comprobar administración de plataforma.
7. Probar creación de un rol personalizado, asignación a un usuario y revocación.
8. Confirmar auditoría y ejecutar Supabase Security Advisor nuevamente.

## Criterio de salida

El bloque puede pasar a rollout cuando AppShell esté verde, la autoridad contextual estricta tenga cobertura de pruebas, la API mínima de Seguridad y acceso esté operativa, el bootstrap inicial esté revisado y CI/Sonar estén completamente verdes. Hasta entonces no se aplican las migraciones RBAC en producción.
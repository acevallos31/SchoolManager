# Bloque 042 — Preflight previo al rollout RBAC

## Estado

Las migraciones 028–035 permanecen sin aplicar en Supabase. Producción registra la cadena 007–027 y el baseline histórico. El PR #97 sigue en Draft.

El último CI del head ec0c13d853e74cf1701be4fe1ecec7ea95e23bca mantiene backend, API, base de datos y runtime Vercel en verde. El frontend tiene dos pruebas fallidas del AppShell relacionadas con actualización visual del contexto institucional bajo zoneless change detection.

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

## Regla de autoridad institucional

La función genérica histórica de permisos acepta asignaciones globales como fallback para compatibilidad. Esa semántica debe conservarse temporalmente para módulos legacy, pero no debe reutilizarse como autoridad de administración RBAC institucional.

Las operaciones de Seguridad y acceso deben usar una comprobación contextual estricta: asignación activa en la institución exacta con el permiso solicitado, o platform_admin global con el permiso correspondiente. Esto aplica a crear/clonar/editar/desactivar roles, reemplazar permisos, asignar roles a usuarios y retirar asignaciones institucionales.

## API mínima de Seguridad y acceso

La primera superficie administrativa debe exponer lecturas de roles institucionales, plantillas globales disponibles, permisos delegables y asignaciones de usuarios para una institución explícita. Las escrituras deben envolver las RPC transaccionales 029–035 y conservar la autoridad final en PostgreSQL.

La ruta frontend prevista es `Configuración > Seguridad y acceso`, visible por capacidades de identidad y no por nombres fijos de rol.

## Bootstrap del primer Superadministrador

El primer platform_admin será una operación de rollout privilegiada, manual y auditada. Debe ejecutarse únicamente cuando no exista ya un Superadministrador activo, contra un usuario interno activo y después de confirmar explícitamente la cuenta destino. Las altas posteriores se harán mediante el flujo normal protegido para Superadministradores.

## Criterio de salida

El bloque puede pasar a rollout cuando AppShell esté verde, la autoridad contextual estricta tenga cobertura de pruebas, la API mínima de Seguridad y acceso esté operativa, el bootstrap inicial esté revisado y CI/Sonar estén completamente verdes. Hasta entonces no se aplican las migraciones RBAC en producción.
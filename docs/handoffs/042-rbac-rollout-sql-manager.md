# Bloque 042 — Rollout RBAC desde SQL Manager

Estado: listo para ejecución manual controlada
Rama: `feature/rbac-dinamico-institucional-042`

## Objetivo

Aplicar en producción el bloque RBAC dinámico institucional desde un SQL Manager, manteniendo el orden exacto de migraciones y verificando el resultado antes del bootstrap del primer Superadministrador.

## 1. Preflight

Ejecutar primero:

`database/operations/042_rbac_sql_manager_preflight.sql`

Antes de continuar debe confirmarse:

- existe `027` en `public.schema_migrations`;
- no existe ninguna versión `028`..`039` si es el primer intento;
- si existe alguna de esas versiones, detenerse y revisar si hubo una aplicación parcial;
- registrar/conservar el conteo de roles, permisos y asignaciones activas como evidencia previa.

## 2. Implementación

Abrir y ejecutar **uno por uno** los siguientes archivos en el SQL Manager. No ejecutar fuera de orden y no omitir ninguno:

1. `database/migrations/028_roles_dinamicos_institucionales.sql`
2. `database/migrations/029_operaciones_roles_institucionales.sql`
3. `database/migrations/030_roles_institucionales_clonado_edicion.sql`
4. `database/migrations/031_roles_institucionales_invariantes.sql`
5. `database/migrations/032_roles_institucionales_reemplazar_permisos.sql`
6. `database/migrations/033_roles_institucionales_asignar.sql`
7. `database/migrations/034_roles_institucionales_desactivar.sql`
8. `database/migrations/035_roles_usuario_proteccion_ultimo_admin.sql`
9. `database/migrations/036_rbac_autoridad_institucional_estricta.sql`
10. `database/migrations/037_rbac_lectura_institucional_estricta.sql`
11. `database/migrations/038_rbac_consulta_seguridad_acceso.sql`
12. `database/migrations/039_rbac_canonicalizar_permisos_configuracion_academica.sql`

Cada archivo contiene su propio `BEGIN/COMMIT` y verifica la migración previa. Si uno falla, **no ejecutar el siguiente** hasta revisar el error.

## 3. Validación individual

Después de cada migración puede ejecutarse su archivo correspondiente en:

`database/migrations/validation/`

Ejemplos:

- después de 028: `028_roles_dinamicos_institucionales.validation.sql`;
- después de 036: `036_rbac_autoridad_institucional_estricta.validation.sql`;
- después de 039: `039_rbac_canonicalizar_permisos_configuracion_academica.validation.sql`.

La regla es la misma para 028..039.

## 4. Validación global

Después de aplicar las doce migraciones ejecutar:

`database/operations/042_rbac_sql_manager_validation.sql`

El último bloque debe devolver `resultado = PASS`.

Además debe comprobarse:

- `platform_admin` existe una sola vez, global, protegido y activo;
- los seis permisos `platform.*` son de ámbito `plataforma` y no delegables;
- las plantillas globales existen;
- los aliases internos `configuracion.ciclos.*`, `configuracion.periodos_matricula.*`, `configuracion.grados.*`, `configuracion.jornadas.*` y `configuracion.secciones.*` siguen ocultos/no delegables;
- existen `usuario_tiene_permiso_institucional_estricto`, `rpc_obtener_seguridad_acceso` y `usuario_tiene_permiso_actual`;
- no existen asignaciones de `platform_admin` con `institucion_id` no nulo.

## 5. Primer Superadministrador

No hardcodear correo ni UUID en una migración.

Después de que la validación global dé PASS, resolver en producción el `auth_user_id` del usuario seleccionado y ejecutar:

`database/operations/bootstrap_first_platform_admin.sql`

El usuario seleccionado para este rollout es el propietario operativo de la instalación. El correo se usa únicamente para localizar el registro correcto en Auth; el bootstrap recibe el `auth_user_id` UUID, no el correo.

Antes de ejecutar el bootstrap en la misma sesión SQL:

```sql
set schoolmanager.bootstrap_auth_user_id = '<AUTH_USER_ID_UUID>';
```

Luego ejecutar completo `database/operations/bootstrap_first_platform_admin.sql`.

El script se niega a operar si:

- no está aplicada la 039;
- el UUID no corresponde a un usuario interno activo;
- falta el rol protegido `platform_admin`;
- ya existe un Superadministrador activo.

## 6. Validación posterior al bootstrap

Volver a ejecutar `database/operations/042_rbac_sql_manager_validation.sql`.

La consulta `superadministradores_activos` debe devolver al menos `1`.

También debe existir una fila de auditoría con la acción:

`platform.superadmin.bootstrap_inicial`

## 7. Smoke tests de aplicación

Antes del merge del PR:

- iniciar sesión con el primer Superadministrador;
- confirmar acceso a Configuración → Seguridad y acceso;
- confirmar contexto institucional correcto;
- crear o clonar un rol institucional de prueba;
- modificar permisos delegables;
- asignar y retirar ese rol;
- confirmar que una institución no puede administrar roles de otra;
- confirmar que un admin institucional no puede asignar `platform_admin`;
- confirmar protección del último administrador institucional y último Superadministrador.

## 8. Cierre

Solo cuando migraciones, validaciones, bootstrap y smoke tests estén correctos se procede al merge del PR #97.

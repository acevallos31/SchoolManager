# Bloque 042 — Rollout RBAC desde SQL Manager

Estado: migraciones 028→039 aplicadas y validadas en producción; bootstrap del primer Superadministrador pendiente
Rama: `feature/rbac-dinamico-institucional-042`

## Resultado del rollout productivo

El 2026-09-13 se ejecutó desde PowerShell/psql el rollout completo `028`→`039` contra la base productiva de SchoolManager.

Resultado:

- 12 migraciones aplicadas en orden: `028`→`039`;
- 12 validaciones individuales ejecutadas sin hallazgos;
- `platform_admin` creado como rol global, protegido y activo;
- 6 permisos `platform.*` creados con ámbito `plataforma` y `delegable=false`;
- 9 plantillas globales creadas;
- aliases internos `configuracion.*` académicos permanecen ocultos/no delegables;
- helper estricto, snapshot de Seguridad y acceso y helper de compatibilidad canónica presentes;
- RLS de lectura administrativo presente en `roles`, `permisos`, `roles_permisos` y `usuarios_roles`;
- `seguridad_auditoria` presente;
- asignaciones inválidas de `platform_admin`: `0`;
- Superadministradores activos después de las migraciones: `0`, como se esperaba antes del bootstrap;
- validación global final: `resultado = PASS`.

## Evidencia de `schema_migrations`

Producción registra ahora exactamente estas versiones RBAC:

1. `028_roles_dinamicos_institucionales`
2. `029_operaciones_roles_institucionales`
3. `030_roles_institucionales_clonado_edicion`
4. `031_roles_institucionales_invariantes`
5. `032_roles_institucionales_reemplazar_permisos`
6. `033_roles_institucionales_asignar`
7. `034_roles_institucionales_desactivar`
8. `035_roles_usuario_proteccion_ultimo_admin`
9. `036_rbac_autoridad_institucional_estricta`
10. `037_rbac_lectura_institucional_estricta`
11. `038_rbac_consulta_seguridad_acceso`
12. `039_rbac_canonicalizar_permisos_configuracion_academica`

## Siguiente paso — primer Superadministrador

El usuario seleccionado para el bootstrap inicial es `acevallos31@gmail.com`.

No se debe hardcodear el correo en una migración. Primero hay que resolver el `auth_user_id` real de esa cuenta y confirmar que corresponde a un usuario interno activo.

Después, en la misma sesión SQL:

```sql
set schoolmanager.bootstrap_auth_user_id = '<AUTH_USER_ID_UUID>';
```

Luego ejecutar completo:

`database/operations/bootstrap_first_platform_admin.sql`

El bootstrap exige 039 aplicada, toma un advisory lock, exige cero Superadministradores activos, crea una única asignación global de `platform_admin` y registra `platform.superadmin.bootstrap_inicial` en `seguridad_auditoria`.

## Validación posterior al bootstrap

Volver a ejecutar:

`database/operations/042_rbac_sql_manager_validation.sql`

Debe cumplirse:

- `superadministradores_activos = 1`;
- `asignaciones_platform_admin_invalidas = 0`;
- validación global `resultado = PASS`;
- existe auditoría `platform.superadmin.bootstrap_inicial`.

## Smoke tests antes del merge

- iniciar sesión con el primer Superadministrador;
- confirmar acceso a Configuración → Seguridad y acceso;
- confirmar contexto institucional correcto;
- crear o clonar un rol institucional de prueba;
- modificar permisos delegables;
- asignar y retirar ese rol;
- confirmar aislamiento entre instituciones;
- confirmar que un admin institucional no puede asignar `platform_admin`;
- confirmar protección del último administrador institucional y del último Superadministrador.

## Cierre

No hacer merge del PR #97 hasta completar bootstrap, validación posterior y smoke tests reales de aplicación.

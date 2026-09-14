# Bloque 042 — Rollout RBAC desde SQL Manager

Estado: **rollout 028→039 completado y validado; bootstrap inicial completado**
Rama: `feature/rbac-dinamico-institucional-042`
Fecha de ejecución: 2026-09-14

## Objetivo

Aplicar el bloque RBAC dinámico institucional manteniendo el orden exacto de migraciones, validando cada paso y creando de forma explícita y auditada el primer Superadministrador.

## Resultado final

El rollout se ejecutó correctamente desde PowerShell/psql.

Resultado confirmado:

- migraciones `028`→`039`: aplicadas en orden;
- validaciones individuales `028`→`039`: PASS;
- validación global: `resultado = PASS`;
- `platform_admin`: una definición global, protegida y activa;
- permisos `platform.*`: 6, todos de ámbito plataforma y no delegables;
- plantillas globales: 9;
- aliases internos `configuracion.*` de ciclos/estructura: ocultos y no delegables;
- helpers/RPC esperados presentes;
- asignaciones inválidas de `platform_admin`: 0;
- Superadministradores activos después del bootstrap: 1;
- auditoría del bootstrap inicial: registrada.

## 1. Preflight ejecutado

Antes del rollout se confirmó:

- `027` presente en `public.schema_migrations`;
- ninguna `028`..`039` aplicada previamente;
- `roles = 7`;
- `permisos = 72`;
- `asignaciones_activas = 3`;
- `instituciones_activas = 1`;
- `usuarios_activos = 3`;
- columnas nuevas de 028 ausentes antes de iniciar.

Archivo operativo:

`database/operations/042_rbac_sql_manager_preflight.sql`

## 2. Implementación aplicada

Se ejecutaron, una por una y en orden, las siguientes migraciones:

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

Cada migración confirmó `COMMIT` y quedó registrada en `public.schema_migrations`.

## 3. Validaciones individuales

Después de cada migración se ejecutó su validación correspondiente bajo:

`database/migrations/validation/`

Las doce validaciones finalizaron sin hallazgos bloqueantes.

## 4. Validación global posterior a 039

Se ejecutó:

`database/operations/042_rbac_sql_manager_validation.sql`

Resultado confirmado:

- 12 versiones `028`..`039` registradas;
- ninguna versión faltante;
- `platform_admin` único/global/protegido/activo;
- seis permisos `platform.*` vigentes;
- nueve plantillas activas;
- veinte aliases internos `configuracion.*` permanecen no delegables y ocultos;
- presentes `usuario_tiene_permiso_institucional_estricto`, `rpc_obtener_seguridad_acceso` y `usuario_tiene_permiso_actual`;
- cuatro políticas RLS administrativas presentes;
- `seguridad_auditoria` presente;
- asignaciones inválidas de `platform_admin`: 0;
- resultado final: `PASS`.

## 5. Bootstrap inicial de Superadministrador

Se verificó primero que la identidad seleccionada correspondiera a un usuario interno activo.

Se ejecutó:

`database/operations/bootstrap_first_platform_admin.sql`

El bootstrap:

- exigió 039 aplicada;
- confirmó cero Superadministradores activos previos;
- confirmó usuario interno activo;
- creó una asignación global activa de `platform_admin`;
- registró auditoría `platform.superadmin.bootstrap_inicial`;
- completó con `COMMIT`.

## 6. Validación posterior al bootstrap

Resultado confirmado después del bootstrap:

- `superadministradores_activos = 1`;
- asignación activa global de `platform_admin`: 1;
- `institucion_id` de esa asignación: `NULL`;
- auditoría `platform.superadmin.bootstrap_inicial`: presente;
- `asignaciones_platform_admin_invalidas = 0`;
- validación global: `resultado = PASS`.

## 7. Smoke tests pendientes de aplicación

El rollout de base de datos está cerrado. Antes del merge del PR deben completarse los smoke tests de aplicación:

- iniciar sesión con el primer Superadministrador;
- confirmar acceso a **Configuración → Seguridad y acceso**;
- confirmar contexto institucional correcto;
- crear o clonar un rol institucional de prueba;
- modificar permisos delegables;
- asignar y retirar ese rol;
- confirmar que una institución no puede administrar roles de otra;
- confirmar que un admin institucional no puede asignar `platform_admin`;
- confirmar protección del último administrador institucional y del último Superadministrador.

## 8. Cierre

- migraciones: **COMPLETADAS**;
- validaciones DB: **PASS**;
- bootstrap inicial: **COMPLETADO**;
- auditoría bootstrap: **CONFIRMADA**;
- smoke tests de aplicación: **PENDIENTES**;
- merge PR #97: **PENDIENTE hasta finalizar smoke tests**.

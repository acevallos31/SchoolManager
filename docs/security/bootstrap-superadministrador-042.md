# Bootstrap del primer Superadministrador — Bloque 042

## Objetivo

`platform_admin` es un rol global protegido y la migración 028 no lo asigna automáticamente a ningún usuario. El primer Superadministrador debe crearse una sola vez durante el rollout, de forma explícita y auditable, sin promover por defecto a un `admin` legacy.

El procedimiento está implementado en:

`database/operations/bootstrap_first_platform_admin.sql`

Ese archivo **no es una migración** y no forma parte de la cadena automática 001→038.

## Precondiciones obligatorias

Antes de ejecutar el bootstrap deben cumplirse todas estas condiciones:

1. La base objetivo tiene aplicadas las migraciones 028→038 en orden y sus validaciones terminaron sin hallazgos.
2. El usuario elegido ya existe en `public.usuarios`, está activo y tiene un `auth_user_id` válido vinculado a Supabase Auth.
3. No existe ningún `platform_admin` activo. El script se bloquea si detecta uno.
4. Se conserva evidencia del `auth_user_id` elegido y del operador que autorizó la promoción fuera de la base de datos, por ejemplo en el ticket/runbook de despliegue.
5. Se dispone del backup/export previo al rollout y todavía no se habilitó el editor RBAC para usuarios finales.

## Preflight de solo lectura

Reemplazar `<AUTH_USER_ID_UUID>` únicamente para las consultas de verificación:

```sql
select u.id, u.auth_user_id, u.activo, p.nombres, p.apellidos
from public.usuarios u
left join public.personas p on p.id = u.persona_id
where u.auth_user_id = '<AUTH_USER_ID_UUID>'::uuid;

select count(*) as superadmins_activos
from public.usuarios_roles ur
join public.roles r on r.id = ur.rol_id
join public.usuarios u on u.id = ur.usuario_id
where ur.activo
  and ur.institucion_id is null
  and r.activo
  and r.tipo = 'plataforma'
  and r.codigo = 'platform_admin'
  and u.activo;

select version, nombre
from public.schema_migrations
where version between '028' and '038'
order by version;
```

El resultado esperado antes del bootstrap es: usuario objetivo único y activo, `superadmins_activos = 0` y versiones 028→038 presentes.

## Ejecución controlada

En la misma sesión SQL se define el `auth_user_id` objetivo y después se ejecuta el archivo operativo completo:

```sql
set schoolmanager.bootstrap_auth_user_id = '<AUTH_USER_ID_UUID>';
```

Después ejecutar `database/operations/bootstrap_first_platform_admin.sql`.

El script abre su propia transacción, toma el mismo advisory lock de Superadministrador utilizado por la protección de la migración 035, vuelve a validar todas las precondiciones, crea una única asignación global y registra `platform.superadmin.bootstrap_inicial` en `seguridad_auditoria`.

Si ya existe un Superadministrador, si el usuario no está activo/vinculado o si 038 no está registrada, la transacción falla y no deja una asignación parcial.

## Verificación posterior

```sql
select u.id as usuario_id, r.codigo, ur.id as asignacion_id, ur.activo, ur.created_at
from public.usuarios_roles ur
join public.usuarios u on u.id = ur.usuario_id
join public.roles r on r.id = ur.rol_id
where r.codigo = 'platform_admin'
  and r.tipo = 'plataforma'
  and ur.institucion_id is null
  and ur.activo;

select accion, entidad_tipo, entidad_id, detalle, created_at
from public.seguridad_auditoria
where accion = 'platform.superadmin.bootstrap_inicial'
order by created_at desc
limit 5;
```

Debe existir exactamente un Superadministrador activo y una entrada de auditoría correspondiente al bootstrap.

## Después del bootstrap

El archivo de bootstrap no debe volver a utilizarse. Las siguientes altas o bajas de Superadministradores deben realizarse mediante las operaciones protegidas de plataforma, conservando la regla de que nunca puede retirarse el último Superadministrador activo.

Este runbook **no autoriza todavía el rollout de 028→038**. Mientras el puente entre permisos canónicos (`academico.ciclos.*`, `academico.estructura.*`) y los aliases internos históricos de las RPC siga pendiente, producción debe permanecer en 027.

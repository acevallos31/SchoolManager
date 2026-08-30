# Seguridad PostgreSQL y Supabase

## Identidad y autoridad

Las policies y RPC obtienen la identidad exclusivamente mediante `auth.uid()`:

```text
auth.uid()
  -> usuarios.auth_user_id
  -> usuarios_roles activos
  -> roles activos
  -> roles_permisos
  -> permiso + ámbito institucional
```

Correo, claims `role`, metadata, `usuario_id` e `institucion_id` enviados por el
cliente no son autoridad. Una Institución recibida como parámetro solo se usa
después de comprobar que el usuario posee el permiso en ese ámbito o mediante un
rol global con el mismo permiso.

## Roles SQL de Supabase

- `anon`: no recibe acceso a datos privados ni ejecución de RPC de negocio.
- `authenticated`: SELECT sobre tablas protegidas por RLS y EXECUTE únicamente
  sobre helpers/RPC aprobados. No posee INSERT, UPDATE ni DELETE directo.
- `service_role`: conserva privilegios administrativos y BYPASSRLS en Supabase;
  nunca debe exponerse en frontend.

RLS complementa GRANT/REVOKE; no los reemplaza.

## Lecturas y operaciones

- Consultas simples: PostgREST + RLS.
- Procesos transaccionales: `rpc_*`.
- Angular actual: JWT hacia API .NET; la API sigue siendo responsable de
  autorización mientras use conexión directa.
- Expo futuro: JWT Supabase, PostgREST para SELECT y RPC para mutaciones.

`authenticated` no puede escribir directamente Alumnos, Secciones, Matrículas,
Historial ni RBAC. Tampoco puede ejecutar las funciones núcleo.

## SECURITY INVOKER y SECURITY DEFINER

Las funciones núcleo (`crear_seccion`, `matricular_alumno`, etc.) permanecen
`SECURITY INVOKER`. Su EXECUTE se revoca a clientes.

Las RPC son `SECURITY DEFINER` porque `authenticated` no tiene privilegios de
escritura. Cada wrapper:

1. fija `search_path = pg_catalog, public, pg_temp`;
2. resuelve el Usuario desde `auth.uid()`;
3. exige Usuario, Rol y asignación activos;
4. verifica permiso en la Institución derivada de los datos;
5. obtiene internamente el actor;
6. llama la función transaccional existente.

`usuario_tiene_permiso` también es DEFINER para leer RBAC sin recursión RLS, pero
no se concede directamente a `authenticated`. Los helpers que sí se conceden
solo evalúan la identidad actual.

## Aislamiento institucional

Un rol institucional solo actúa en su Institución. Un rol global aporta permisos
en cualquier Institución únicamente si `roles_permisos` contiene el permiso; no
existe un atajo hardcodeado para Admin.

Las policies varían por tabla:

- Alumno: permiso institucional, perfil propio o relación Responsable activa.
- Responsable: permiso institucional o perfil propio.
- Matrícula/Historial: permiso institucional o acceso contextual al Alumno.
- Ciclo/Sección/Periodo: permisos académicos en la Institución derivada.
- RBAC: lectura propia o permisos administrativos según ámbito.

Padre no recibe `academico.alumnos.ver` global. Solo ve representados vinculados
por `responsables` + `alumno_responsable`. Un usuario Alumno puede ver el perfil
cuya `persona_id` coincide con la suya.

## Gestión RBAC

`rpc_asignar_rol_usuario` y `rpc_desactivar_rol_usuario` exigen
`identidad.usuarios.asignar_roles` en el ámbito correspondiente. Desactivar exige
motivo y conserva la asignación histórica; no existe DELETE cliente.

## Backend .NET

La API lee `ConnectionStrings__PostgreSQL`; el código no fija ni cambia el rol
SQL. En Supabase debe conectarse como propietario/BYPASSRLS o mediante un futuro
rol técnico explícitamente privilegiado. No debe conectarse como `authenticated`.

El propietario puede continuar con SQL directo y no se rompe en este bloque.
Las mutaciones críticas futuras deberían invocar las funciones núcleo para
compartir atomicidad e invariantes con las RPC. Si se crea un rol técnico no
propietario, sus grants deben declararse explícitamente y nunca reutilizar
`service_role` en clientes.

`GET /api/auth/me` devuelve únicamente `id`, `personaId`, `roles` y `permisos`.
Roles y permisos son la unión efectiva de todas las asignaciones activas,
globales e institucionales. La respuesta no implica una institución
seleccionada y no sustituye la comprobación contextual de cada operación.

## Datos que nunca se guardan como autoridad frontend

- claves `service_role` o credenciales PostgreSQL;
- rol o permiso decidido localmente;
- `usuario_id` actor;
- Institución autorizada sin validación servidor;
- resultados de autorización persistidos como fuente de verdad.

## Rollback

El rollback 009 retira policies y RPC, restaura las funciones núcleo y deja
`authenticated` sin acceso a tablas. No intenta reconstruir grants previos
específicos de cada proyecto, porque PostgreSQL no conserva ese estado histórico.

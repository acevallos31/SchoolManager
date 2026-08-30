# RBAC de SchoolManager

## Conceptos

Un **rol** es un conjunto estable de capacidades con un `codigo` usado por el
sistema y un `nombre` de presentación. Un **permiso** representa una acción
atómica autorizable. Los usuarios reciben uno o más roles globales o acotados a
una institución; no reciben autoridad desde claims `role` de Supabase.

Los códigos de permiso usan obligatoriamente:

```text
modulo.recurso.accion
```

Ejemplos: `academico.alumnos.ver` e
`identidad.usuarios.asignar_roles`. Los checks de PostgreSQL exigen minúsculas,
tres segmentos y coherencia entre `permisos.modulo` y el primer segmento.

## Ámbitos y unicidad

`usuarios_roles.institucion_id IS NULL` significa rol global. Un UUID significa
que el rol solo aporta permisos en esa institución. Dos índices parciales
impiden duplicar una asignación activa:

- `(usuario_id, rol_id)` para el ámbito global;
- `(usuario_id, rol_id, institucion_id)` para el ámbito institucional.

Las asignaciones inactivas pueden repetirse porque representan periodos
históricos distintos. Al desactivarlas se exigen fecha y motivo; no se eliminan.

Un usuario Docente + Padre tendrá dos asignaciones. También puede ser Docente en
una institución y Administrador en otra. El selector de institución y la forma
de validar membresía pertenecen a una fase posterior; nunca se confiará en un
`institucion_id` enviado por el cliente sin resolverlo contra datos autorizados.

## Matriz inicial

| Rol | Estado en este bloque | Permisos |
| --- | --- | --- |
| Administrador | operativo | conjunto base completo |
| Operador | operativo | ver/crear/editar alumnos; ver/crear matrículas; ver/crear/editar responsables |
| Padre | compatibilidad | sin permiso genérico hasta definir alcance por representado |
| Usuario | compatibilidad legacy | sin permisos iniciales; significado pendiente |
| Docente | reservado | sin permisos hasta implementar Docencia |
| Cajero | reservado | `pagos.ver` y `pagos.registrar` cuando exista Finanzas; nunca se presuponen editar/anular |
| Consulta | disponible | se le podrán asignar permisos `*.ver`, sin mutaciones |

No se sembraron permisos financieros, docentes ni de reportes porque sus
módulos todavía no existen. En particular, dar a Padre un permiso genérico de
lectura sin filtro por `alumno_responsable` permitiría acceso excesivo.

## Migración desde `usuarios.rol`

La migración 007 crea los cuatro roles legacy (`admin`, `operador`, `usuario` y
`padre`) y convierte cada valor en una asignación global. También crea roles
reservados para evolución conocida. Falla si encuentra un código legacy
desconocido, de modo que ningún usuario se pierda silenciosamente.

La migración 010 verifica que cada valor legacy tenga una asignación RBAC
activa equivalente y luego elimina `usuarios.rol`. El backend resuelve todos
los roles y permisos efectivos; Angular puede usar roles para navegación, pero
nunca como frontera de seguridad.

El rollback 010 solo reconstruye la columna cuando cada usuario posee
exactamente una asignación global activa entre `admin`, `operador`, `usuario` o
`padre`. Ante multirol o ámbito institucional aborta para no elegir datos
arbitrariamente.

El código legacy `usuario` se conserva únicamente por compatibilidad hasta que
producto defina si será Consulta, un usuario sin asignación o un rol real.

### Baseline frente a migración incremental

- `database/baseline/001_schoolmanager_fase1a.sql` conserva su nombre físico
  histórico, pero instala directamente el estado consolidado de Fase 1C y no
  contiene `usuarios.rol`.
- `database/migrations/007_crear_rbac_base.sql` evoluciona una instalación que
  ya pasó por 001–006. Además de crear el mismo modelo, valida todos los valores
  de `usuarios.rol` y genera una asignación global por cada usuario existente.

Una instalación nueva usa únicamente el baseline. Una instalación existente
aplica 007, 008, 009 y 010 en orden; 010 se ejecuta después de desplegar el
backend multirol.

## Consulta de autorización

`usuario_tiene_permiso(auth_user_id, permiso, institucion_id)` es una función
SQL `STABLE` y `SECURITY DEFINER`. Exige usuario, asignación y rol activos. Sin
institución consulta solo roles globales; con institución combina roles globales
y roles del ámbito exacto. Una identidad o permiso inexistente devuelve `false`.

El uso de `SECURITY DEFINER` está acotado a lectura RBAC, con `search_path`
explícito y sin `EXECUTE` directo para `authenticated`. Esto evita recursión RLS
y permite que las policies consulten roles aunque el cliente no pueda leer o
modificar tablas RBAC libremente. Los helpers públicos siempre usan `auth.uid()`.

## Integración futura

- **.NET:** resuelve `sub -> usuarios.auth_user_id` y agrega todos los roles y
  permisos activos. `/api/auth/me` no selecciona ni finge un ámbito
  institucional; la autorización contextual sigue validándose en PostgreSQL.
- **Supabase RLS/RPC:** resuelve siempre con `auth.uid()`; las policies añaden
  alcance institucional, representados y perfil propio.
- **React Native/Expo:** no enviará roles confiables; consumirá RLS y RPC con el
  JWT de Supabase.

Un permiso expresa capacidad, no alcance de filas. RBAC por sí solo no autoriza
a un padre a consultar cualquier alumno.

## Política de borrado

- `usuarios_roles -> usuarios`, `roles`, `instituciones`: `RESTRICT`, porque la
  asignación tiene valor histórico y se desactiva lógicamente.
- `roles_permisos -> roles`, `permisos`: `CASCADE`, porque es una composición
  vigente sin identidad ni histórico propio.

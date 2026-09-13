# 042A — Inventario de permisos y delegación RBAC

Fecha: 2026-09-13
Estado: inventario inicial contra `main` en `8bd97df`

## Resultado

El catálogo actual contiene **72 códigos de permiso** sembrados por las migraciones activas. `Permisos.cs` declara 34 permisos de aplicación usados por la capa .NET. El resto corresponde principalmente a autorización interna de RPC/DB, compatibilidad histórica o permisos que todavía no están expuestos como políticas .NET.

Este inventario no renombra ni elimina permisos. Define qué debe ser visible al administrador de una institución y qué queda reservado a SchoolManager.

## Catálogo actual agrupado

| Grupo | Acciones | Cantidad | Clasificación propuesta |
| --- | --- | ---: | --- |
| `academico.alumnos.*` | ver, crear, editar, desactivar | 4 | institucional, delegable |
| `academico.matriculas.*` | ver, crear, editar, anular, cambiar_estado | 5 | institucional; editar/anular requieren auditoría de uso |
| `responsables.responsables.*` | ver, crear, editar | 3 | legacy/deprecado, no mostrar |
| `identidad.usuarios.*` | ver, crear, editar, asignar_roles | 4 | institucional, alto riesgo |
| `identidad.roles.*` | ver, crear, editar, asignar_permisos | 4 | institucional, alto riesgo |
| `academico.secciones.*` | ver, crear, editar, desactivar | 4 | interno/compatibilidad; revisar frente a estructura |
| `academico.responsables.*` | ver, crear, editar | 3 | institucional, delegable; namespace vigente |
| `configuracion.sistema.*` | ver, editar | 2 | plataforma, no delegable |
| `configuracion.instituciones.*` | ver, editar | 2 | plataforma, no delegable en su semántica actual |
| `configuracion.ciclos.*` | ver, crear, editar, desactivar | 4 | interno DB; no mostrar directamente |
| `configuracion.periodos_matricula.*` | ver, crear, editar, desactivar | 4 | interno DB; no mostrar directamente |
| `configuracion.grados.*` | ver, crear, editar, desactivar | 4 | interno DB; no mostrar directamente |
| `configuracion.jornadas.*` | ver, crear, editar, desactivar | 4 | interno DB; no mostrar directamente |
| `configuracion.secciones.*` | ver, crear, editar, desactivar | 4 | interno DB; no mostrar directamente |
| `configuracion.conceptos_financieros.*` | ver, crear, editar, desactivar | 4 | institucional, delegable |
| `configuracion.planes_pago.*` | ver, crear, editar, desactivar | 4 | institucional, delegable |
| `academico.cargos.*` | ver, generar, anular | 3 | institucional, delegable |
| `academico.pagos.*` | ver, registrar, anular | 3 | institucional, alto riesgo financiero |
| `academico.ciclos.*` | ver, crear, editar, desactivar | 4 | institucional, superficie de aplicación |
| `academico.estructura.*` | ver, editar, desactivar | 3 | institucional, superficie de aplicación |

Total: **72**.

## Permisos reservados de plataforma

Los códigos existentes siguientes representan operaciones de implementación/plataforma y no deben quedar disponibles para un `school_admin` simplemente porque antes el rol `admin` los poseía:

- `configuracion.sistema.ver`
- `configuracion.sistema.editar`
- `configuracion.instituciones.ver`
- `configuracion.instituciones.editar`

La semántica actual de `configuracion.instituciones.editar` incluye crear y modificar instituciones desde la API. Por ello se clasifica como plataforma. Más adelante, si una institución necesita editar únicamente su propio perfil, deberá existir una capacidad institucional distinta y acotada; no se debe reutilizar el permiso global.

## Permisos de identidad institucional

Estos permisos seguirán disponibles para administración institucional, pero con validación de contexto y cota de delegación:

- `identidad.usuarios.ver`
- `identidad.usuarios.crear`
- `identidad.usuarios.editar`
- `identidad.usuarios.asignar_roles`
- `identidad.roles.ver`
- `identidad.roles.crear`
- `identidad.roles.editar`
- `identidad.roles.asignar_permisos`

Tener uno de estos permisos nunca habilita operaciones globales. En particular, `identidad.usuarios.asignar_roles` no puede permitir asignar `platform_admin`.

## Nuevos permisos de plataforma requeridos

El bloque incorporará permisos explícitos y no delegables:

- `platform.superadmins.gestionar`
- `platform.roles.ver`
- `platform.roles.editar`
- `platform.permisos.ver`
- `platform.permisos.editar`
- `platform.auditoria.ver`

Todos tendrán ámbito `platform`, riesgo crítico/alto según operación y `delegable = false`. Se asignarán exclusivamente a `platform_admin`.

## Hallazgos de compatibilidad

### 1. Namespace de responsables

`responsables.responsables.*` nació en 007. El namespace vigente es `academico.responsables.*`, creado en 009 y usado por la seguridad actual. Los tres permisos legacy se conservan temporalmente para compatibilidad, pero deben marcarse como deprecados y no aparecer en la pantalla de roles.

### 2. Ciclos tiene dos capas de permisos

La aplicación .NET usa `academico.ciclos.*`, mientras las RPC históricas de 014 validan `configuracion.ciclos.*` y `configuracion.periodos_matricula.*`.

Para un administrador institucional no es aceptable tener que conocer esta duplicidad. La UI debe presentar una capacidad funcional única. Antes del editor de roles se debe resolver una de estas opciones de forma explícita:

- canonicalizar las RPC hacia los permisos de aplicación conservando alias temporales; o
- mantener un mapeo central que expanda una capacidad visible a los permisos internos requeridos.

No se implementará lógica duplicada dispersa en Angular.

### 3. Estructura académica también tiene dos capas

La aplicación .NET usa `academico.estructura.*`, mientras las RPC de estructura usan `configuracion.grados.*`, `configuracion.jornadas.*` y `configuracion.secciones.*`.

La misma regla anterior aplica: los permisos internos no se mostrarán como opciones independientes al usuario hasta cerrar la canonicalización/mapeo.

### 4. Permisos históricos de matrícula

`academico.matriculas.editar` y `academico.matriculas.anular` aparecen en el catálogo inicial, pero la aplicación actual declara `academico.matriculas.cambiar_estado` como capacidad vigente. Antes de mostrarlos en el editor se debe verificar si tienen consumidores productivos. Hasta entonces quedan como `compatibilidad/no exponer`.

### 5. Secciones históricas

`academico.secciones.*` existe desde la capa de seguridad 009, mientras la administración de estructura evolucionó después a `academico.estructura.*` + permisos internos `configuracion.secciones.*`. No se expondrá este grupo de forma independiente hasta confirmar su uso real.

## Hardcoding de `admin` detectado

La auditoría encontró asignación automática de nuevos permisos al rol `admin` en las migraciones:

- 007 — catálogo RBAC base completo;
- 009 — seguridad/RPC;
- 012 — configuración del sistema e instituciones;
- 014 — ciclos y períodos;
- 016 — grados, jornadas y secciones;
- 018 — configuración financiera;
- 019 — cargos;
- 021 — pagos;
- 023 — permisos de aplicación para ciclos;
- 024 — permisos de aplicación para estructura.

Este patrón no debe repetirse. A partir del RBAC dinámico, una migración nueva registra permisos y actualiza plantillas explícitas; nunca debe conceder todo permiso nuevo a un rol humano por nombre.

## Hardcoding de roles en frontend detectado

Existen dos rutas que todavía dependen del nombre del rol:

- login: `admin -> /dashboard`, `padre -> /portal-padre`, otro rol -> error + logout;
- callback OAuth: `padre -> /portal-padre`, cualquier otro -> `/dashboard`.

`AuthService.tieneRol()` también permanece disponible. El servicio puede conservar roles como información, pero la ruta inicial debe pasar a una resolución central basada en capacidades/contexto.

## Regla para la pantalla Configuración > Seguridad y acceso

La pantalla de una institución solo mostrará permisos funcionales aprobados como visibles/delegables. No mostrará:

- `platform.*`;
- permisos deprecados;
- permisos internos de DB usados únicamente como compatibilidad;
- capacidades que el administrador actual no posee;
- permisos marcados como no delegables.

La superficie exclusiva de Superadministrador sí podrá administrar metadatos del catálogo, plantillas globales y otros Superadministradores.

## Próximo cambio técnico

La migración 028 será aditiva y deberá preparar metadatos de roles/permisos y el rol protegido `platform_admin` sin convertir automáticamente ningún `admin` existente en Superadministrador. El primer Superadministrador de una instalación se asignará de forma explícita y auditada durante el rollout; nunca por inferencia del rol legacy.

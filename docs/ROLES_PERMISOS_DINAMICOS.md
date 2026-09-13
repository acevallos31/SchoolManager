# Diseño objetivo: roles y permisos dinámicos por institución

**Estado:** propuesta inicial para el siguiente bloque de autorización  
**Fecha:** 2026-09-13  
**No aplicado:** este documento no crea ni modifica tablas, roles, permisos o asignaciones en Supabase.

## 1. Decisión de producto

Los **permisos** pertenecen al producto y representan capacidades técnicas que el backend sabe validar. Los **roles institucionales** son configurables por cada institución.

Una institución podrá:

- activar roles base para su operación;
- clonar un rol base;
- cambiar nombre y descripción;
- agregar o retirar permisos disponibles para su institución;
- crear roles propios como Secretaría, Coordinación, Caja o Consulta;
- activar, desactivar y asignar esos roles a sus usuarios;
- conservar historial de asignaciones y cambios.

Una institución **no** podrá:

- modificar el catálogo global de permisos;
- crear permisos arbitrarios que el backend no conozca;
- modificar las plantillas globales ni los roles de plataforma;
- asignar permisos de otra institución;
- asignarse el rol platform_admin;
- saltarse las restricciones de datos, RLS o las relaciones padre/alumno.

“Modificar el catálogo base” significa personalizar la copia institucional de una plantilla. El catálogo global se conserva protegido para no cambiar el comportamiento de todas las escuelas al modificar una sola.

## 2. Estado actual que condiciona el diseño

El modelo actual ya contiene:

- las tablas roles, permisos, roles_permisos y usuarios_roles;
- usuarios_roles.institucion_id para asignaciones institucionales;
- evaluación SQL del permiso con usuario_tiene_permiso;
- autorización .NET basada en permisos.

La limitación actual es que roles.codigo es único globalmente y la definición del rol no tiene dueño institucional. La migración inicial también sembró nombres legacy como admin, operador, padre, docente, cajero y consulta.

Además, el login actual todavía decide la ruta con una lista fija de roles (admin y padre). Eso debe reemplazarse por permisos, capacidades y una ruta calculada para que un rol institucional nuevo no termine como “rol no reconocido”.

Existe también una deuda de nomenclatura que debe auditarse antes de migrar: algunas migraciones históricas usan prefijos como responsables.responsables.*, mientras el backend actual usa academico.responsables.*. No se deben renombrar permisos en producción sin inventario, alias temporal y pruebas.

## 3. Modelo conceptual objetivo

| Elemento | Propietario | Mutabilidad | Alcance |
| --- | --- | --- | --- |
| Permiso | SchoolManager | Versionado por desarrollo | Global |
| Plantilla de rol | SchoolManager | Protegida; cambia mediante versión/migración | Global |
| Rol institucional | Administrador de la institución | Editable dentro de su institución | Una institución |
| Asignación de rol | Institución o plataforma según alcance | Historizable; baja lógica | Usuario + institución |
| Identidad de máquina | Plataforma/institución | Configurable y auditable | Herramientas autorizadas |

### 3.1 Tipos de rol

Se propone distinguir tres tipos de definición:

1. **Plataforma:** roles globales del proveedor, especialmente platform_admin.
2. **Plantilla:** roles base que SchoolManager publica para ser clonados.
3. **Institucional:** copia o rol personalizado propiedad de una institución.

Las plantillas no deben asignarse directamente a usuarios. Al activar una plantilla se crea una definición institucional con referencia a su origen y con una fotografía de sus permisos iniciales.

## 4. Evolución de datos propuesta

La siguiente estructura es objetivo de diseño, no una migración lista para ejecutar.

### roles

Conservar los campos actuales y añadir, sujeto a revisión de nombres:

- institucion_id nullable;
- tipo de definición: plataforma, plantilla o institucional;
- rol_base_id nullable para conocer la plantilla de origen;
- indicador de protección;
- versión o marca de sincronización de plantilla;
- fecha y usuario de última modificación.

Reglas de unicidad:

- las plantillas y roles de plataforma tienen código único global;
- los roles institucionales tienen código único dentro de su institución;
- el nombre visible puede personalizarse, pero no sustituye al identificador interno;
- un rol desactivado no se borra si conserva historial.

### permisos

Seguirán siendo globales y definidos por el producto. Cada permiso debe tener:

- código estable;
- módulo;
- acción;
- descripción;
- estado vigente/deprecado;
- clasificación de riesgo y si puede ser delegado por un administrador institucional.

El administrador selecciona permisos existentes; no crea códigos ejecutables desde la interfaz.

### roles_permisos

Seguirá representando la composición vigente de un rol. Para cambios sensibles se añadirá auditoría o historial de modificaciones, con:

- quién modificó;
- institución;
- rol;
- permisos agregados/retirados;
- fecha;
- motivo opcional;
- versión resultante.

### usuarios_roles

Mantendrá la asignación historizable actual. Debe reforzarse la regla:

- un rol de institución solo puede asignarse con el mismo institucion_id;
- un rol de plataforma solo puede asignarse en ámbito global;
- una plantilla no puede asignarse directamente;
- las bajas continúan siendo lógicas y con motivo.

La coincidencia de ámbitos debe comprobarse en una RPC o trigger de base de datos, nunca solo en Angular.

## 5. Catálogo inicial de plantillas

Estos son nombres de producto propuestos, no asignaciones automáticas:

| Código de plantilla | Uso |
| --- | --- |
| school_admin | Administración de una institución |
| school_staff | Operación escolar cotidiana |
| academic_coordinator | Coordinación y estructura académica |
| finance_operator | Cargos, pagos y cobranza |
| teacher | Docencia, grupos y calificaciones cuando exista el módulo |
| parent | Portal del responsable |
| student | Portal del alumno cuando exista |
| demo_viewer | Demostración y lectura controlada |
| support_agent | Atención de tickets dentro del alcance permitido |

Rol global reservado:

| Código | Alcance |
| --- | --- |
| platform_admin | Proveedor/desarrollador; administración de plataforma y soporte global |

Los roles de Hermes no se mezclan con los roles humanos:

- development_agent: identidad de máquina para desarrollo, tickets técnicos y CI;
- institution_assistant: identidad de máquina acotada a una institución y a herramientas explícitas.

## 6. Permisos y delegación

Los permisos se organizan por dominio, por ejemplo:

- platform.*;
- identidad.usuarios.*;
- identidad.roles.*;
- institucion.*;
- academico.alumnos.*;
- academico.matriculas.*;
- academico.ciclos.*;
- academico.estructura.*;
- finanzas.*;
- reportes.*;
- tickets.*;
- assistant.*.

El código real debe conservar los nombres ya publicados o migrarlos con compatibilidad. Antes de añadir nuevos módulos se debe cerrar el inventario de códigos existentes y las diferencias de namespace.

Un administrador institucional puede delegar únicamente permisos que:

- pertenecen a su institución;
- están dentro de su propio conjunto efectivo;
- no son globales de plataforma;
- no están marcados como no delegables;
- no dejan a la institución sin un administrador activo.

El backend debe validar esta “cota de delegación” dentro de la misma operación que modifica el rol o la asignación. La interfaz solo presenta opciones; no es la frontera de seguridad.

## 7. Reglas de autorización

1. El backend .NET sigue siendo la autoridad para endpoints.
2. PostgreSQL/RLS/RPC sigue siendo la autoridad para alcance de filas y operaciones transaccionales.
3. Angular usa permisos para navegación y UX, pero no concede acceso.
4. El contexto de institución se resuelve desde membresías autorizadas; no se confía en un UUID enviado por el cliente.
5. Padre y alumno requieren reglas de relación, no solo un permiso genérico.
6. Un rol puede reunir varios permisos, pero un permiso no sustituye el filtro de datos.
7. La cuenta de Google solo autentica la identidad; no define roles ni permisos.
8. No se usan correo, claims externos ni user_metadata como autoridad de autorización.

## 8. Administración de roles

La pantalla y API futuras deben ofrecer:

- listar plantillas activables y roles institucionales;
- clonar plantilla;
- crear rol personalizado;
- editar nombre, descripción y permisos permitidos;
- previsualizar usuarios afectados;
- asignar/desactivar asignaciones;
- mostrar historial y auditoría;
- avisar cuando un cambio quite acceso a un módulo;
- impedir eliminar/desactivar el último administrador institucional;
- probar la matriz efectiva antes de guardar.

Permisos mínimos sugeridos para esta superficie:

- identidad.roles.ver;
- identidad.roles.crear;
- identidad.roles.editar;
- identidad.roles.asignar_permisos;
- identidad.usuarios.asignar_roles.

Estos permisos deberán tener alcance institucional y reglas de delegación; el nombre por sí solo no debe otorgar administración global.

## 9. Compatibilidad con los datos actuales

La migración debe ser explícita y verificable:

| Código actual | Destino propuesto | Tratamiento |
| --- | --- | --- |
| admin | school_admin | Migrar a una asignación institucional después de confirmar la institución |
| operador | school_staff | Migrar conservando sus permisos efectivos |
| padre | parent | Mantener alcance por representados |
| consulta | demo_viewer | Definir permisos de lectura; actualmente puede no tener permisos |
| docente | teacher | Activar cuando exista el módulo |
| cajero | finance_operator | Activar cuando Finanzas esté listo |
| usuario | Pendiente | No migrar por suposición; requiere decisión de producto |

platform_admin no debe asignarse automáticamente a todas las cuentas admin. La cuenta del proveedor/desarrollador se debe identificar y asignar de forma explícita, con auditoría y recuperación segura.

La transición debe mantener alias temporales en backend/frontend durante una versión. No se debe romper el login por cambiar el nombre visible de un rol.

## 10. Ruta de aterrizaje

La ruta inicial no debe depender de una cadena fija de nombres. El backend puede devolver una ruta sugerida basada en permisos y contexto, o el frontend puede resolverla desde un mapa central:

- plataforma: consola de plataforma;
- administración/operación institucional: dashboard escolar;
- parent: portal del responsable;
- student: portal del alumno;
- demo_viewer: dashboard de demostración read-only;
- support_agent: bandeja de tickets;
- rol sin ruta: mensaje de autorización accionable, sin cerrar sesión silenciosamente.

## 11. Fases de implementación

### Fase A — Inventario y contrato

- congelar el catálogo de permisos existentes;
- detectar namespaces duplicados;
- identificar instituciones, usuarios, roles y asignaciones actuales;
- definir permisos delegables/no delegables;
- acordar los roles de plataforma.

### Fase B — Migración de modelo

- añadir ámbito y origen de las definiciones de rol;
- cambiar unicidad de código a global por tipo o por institución;
- reforzar coincidencia entre rol y asignación;
- añadir auditoría;
- crear validaciones y rollback;
- no aplicar en producción sin revisión manual.

### Fase C — Backend

- endpoints/RPC de plantillas, roles y asignaciones;
- chequeo de cota de delegación;
- protección del último administrador;
- respuesta de /api/auth/me con roles, permisos y ámbitos;
- tests de autorización y aislamiento entre instituciones.

### Fase D — Frontend

- pantalla de administración de roles;
- selector de institución autorizado;
- navegación por permisos/capacidades;
- eliminación de condicionales dispersos por nombre;
- tratamiento claro para cuentas demo, padre, alumno y roles nuevos.

### Fase E — Integración Hermes

- herramientas con permisos assistant.*;
- identidad de máquina separada;
- auditoría y límites por institución;
- activación/facturación opcional.

## 12. Pruebas obligatorias

- un administrador no puede editar roles de otra institución;
- no puede asignar una plantilla directamente;
- no puede conceder un permiso que no posee;
- no puede conceder platform.*;
- no puede quitar al último administrador institucional;
- una asignación institucional no puede usar un rol de otra institución;
- un padre solo ve representados autorizados;
- un alumno solo ve su propio alcance;
- un rol nuevo funciona sin cambiar código de navegación;
- desactivar un rol revoca sus permisos efectivos;
- los cambios quedan auditados;
- RLS y API rechazan el mismo caso no autorizado;
- aliases legacy no abren permisos adicionales.

**Siguiente entrega técnica recomendada:** primero inventario de permisos y compatibilidad (admin/operador/padre/consulta), después una migración revisable para roles institucionales. No modificar producción hasta que ese inventario y el rollback estén aprobados.

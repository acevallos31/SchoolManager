# Bloque 042 — RBAC dinámico institucional

Estado: plan aprobado para implementación incremental
Fecha: 2026-09-13
Rama: `feature/rbac-dinamico-institucional-042`

## Objetivo

Evolucionar el RBAC actual para separar claramente la administración global de SchoolManager de la administración de cada institución, permitir roles institucionales configurables y preparar una superficie de Configuración para administrar usuarios, roles y permisos sin depender de nombres de rol hardcodeados.

Este bloque no aplica migraciones automáticamente en Supabase ni modifica producción. Toda migración se revisará, validará y ejecutará manualmente después de CI.

## Decisiones cerradas

1. Los permisos siguen siendo capacidades técnicas controladas por SchoolManager. Una institución puede seleccionar permisos existentes que sean delegables, pero no crear códigos de permiso nuevos desde la interfaz.
2. Los roles institucionales son configurables por cada institución. Una institución podrá activar una plantilla, clonarla, cambiar nombre y descripción, crear roles propios y modificar su composición dentro de los límites permitidos.
3. El rol global protegido se denomina internamente `platform_admin` y se muestra en la interfaz como **Superadministrador**.
4. Solamente un Superadministrador activo puede crear otro Superadministrador, asignar `platform_admin`, retirar `platform_admin` o administrar permisos globales de plataforma.
5. Un administrador institucional nunca puede promoverse a Superadministrador ni promover a otro usuario a Superadministrador.
6. No se puede desactivar ni retirar el rol al último Superadministrador activo.
7. Un administrador institucional solo puede delegar permisos institucionales que posee efectivamente y que estén marcados como delegables.
8. Ningún rol institucional puede contener permisos `platform.*` ni permisos marcados como no delegables.
9. El backend .NET y PostgreSQL/RLS/RPC son la frontera de seguridad. Angular únicamente refleja capacidades para navegación y UX.
10. Las identidades de máquina futuras, como `development_agent` e `institution_assistant`, no se mezclan con los roles humanos.

## Tipos de rol objetivo

### Plataforma

- `platform_admin`: Superadministrador global de SchoolManager.
- Alcance global.
- Protegido.
- No clonable por una institución.
- No asignable por un administrador institucional.

### Plantillas globales

Plantillas mantenidas por SchoolManager y no asignadas directamente a usuarios:

- `school_admin`
- `school_staff`
- `academic_coordinator`
- `finance_operator`
- `teacher`
- `parent`
- `student`
- `demo_viewer`
- `support_agent`

Cuando una institución activa una plantilla, se crea una copia institucional editable con referencia a su origen.

### Institucionales

Roles propiedad de una institución, por ejemplo:

- Secretaría
- Dirección académica
- Coordinación
- Caja
- Biblioteca
- Consulta

El nombre visible es editable. El código interno debe ser único dentro de la institución y no concede autoridad por sí mismo.

## Superficie de Configuración

La aplicación deberá incorporar:

```text
Configuración
└── Seguridad y acceso
    ├── Usuarios
    ├── Roles y permisos
    │   ├── Roles de mi institución
    │   ├── Plantillas disponibles
    │   ├── Permisos delegables
    │   └── Asignaciones
    └── Administración de plataforma
        ├── Plantillas globales
        ├── Catálogo de permisos
        ├── Superadministradores
        └── Auditoría
```

`Administración de plataforma` solo será visible y accesible para `platform_admin`. La ocultación en Angular no sustituye las validaciones de backend y base de datos.

## Matriz de delegación

Cada permiso deberá clasificarse, como mínimo, con estas propiedades conceptuales:

- ámbito: `platform` o `institution`;
- delegable: sí/no;
- riesgo: bajo/medio/alto;
- vigente/deprecado.

Reglas:

- `platform.*` siempre es global y no delegable.
- permisos institucionales sensibles pueden ser no delegables aunque no sean `platform.*`.
- el administrador institucional no puede conceder un permiso que él mismo no posee de forma efectiva.
- la modificación de un rol y su composición debe validarse en una sola operación transaccional.

## Compatibilidad con roles actuales

La transición debe preservar el acceso vigente y no renombrar permisos arbitrariamente.

| Rol actual | Destino objetivo | Regla |
| --- | --- | --- |
| `admin` | `school_admin` | Convertir a rol institucional después de resolver institución |
| `operador` | `school_staff` | Conservar permisos efectivos |
| `padre` | `parent` | Mantener alcance por relación responsable/alumno |
| `consulta` | `demo_viewer` | Solo lectura controlada |
| `docente` | `teacher` | Activar según módulo docente |
| `cajero` | `finance_operator` | Alcance financiero institucional |
| `usuario` | pendiente | No inferir destino sin decisión explícita |

`platform_admin` no se deriva automáticamente de `admin`.

## Navegación y sesión

Se eliminará la dependencia de nombres fijos como `admin` y `padre` para decidir la ruta inicial. La navegación se resolverá por permisos, capacidades y contexto institucional.

Un rol institucional nuevo debe funcionar sin cambios de código en el login. Una cuenta con sesión válida y permisos reconocidos no debe ser expulsada por tener un nombre de rol nuevo.

`GET /api/auth/me` deberá evolucionar posteriormente para exponer el contexto necesario de membresías/ámbitos sin convertir el frontend en autoridad de autorización.

## Fases de implementación

### 042A — Inventario y contrato

- inventariar permisos existentes en migraciones y `Permisos.cs`;
- detectar namespaces duplicados o históricos;
- clasificar permisos por ámbito y delegabilidad;
- identificar todos los lugares que agregan permisos automáticamente a `admin`;
- documentar reglas de compatibilidad.

### 042B — Migración 028 del modelo RBAC

Preparar, sin aplicar en producción:

- `database/migrations/028_roles_dinamicos_institucionales.sql`;
- rollback correspondiente;
- validación correspondiente;
- tests DB.

Modelo objetivo de `roles`:

- `institucion_id` nullable;
- tipo: plataforma, plantilla o institucional;
- `rol_base_id` nullable;
- indicador de protección;
- versión/origen de plantilla;
- auditoría de modificación.

La unicidad de código será global para plataforma/plantillas y por institución para roles institucionales.

### 042C — Operaciones seguras de roles

- crear/clonar rol institucional;
- editar nombre/descripcion;
- modificar permisos dentro de la cota de delegación;
- asignar/desactivar roles;
- impedir mezcla de instituciones;
- impedir asignación directa de plantillas;
- proteger el último administrador institucional;
- proteger el último Superadministrador.

### 042D — Backend .NET

- constantes de permisos de identidad/plataforma;
- endpoints/RPC necesarios;
- autorización por permiso y contexto;
- DTOs explícitos;
- tests de aislamiento, escalación y delegación.

### 042E — Frontend Configuración

- `Configuración > Seguridad y acceso`;
- administración de roles institucionales;
- asignaciones de usuarios;
- superficie exclusiva de Superadministrador;
- navegación basada en permisos/capacidades.

### 042F — Compatibilidad y cierre

- migración controlada de roles legacy;
- alias temporal si es necesario;
- eliminación de checks hardcodeados por nombres de rol;
- pruebas API/DB/frontend;
- handoff final;
- aplicación manual posterior en Supabase solo con aprobación.

## Casos de prueba obligatorios

- un `school_admin` no puede crear ni asignar `platform_admin`;
- un `school_admin` no puede conceder `platform.*`;
- un `school_admin` no puede conceder permisos que no posee;
- un Superadministrador sí puede crear/asignar otro Superadministrador;
- no se puede retirar/desactivar el último Superadministrador activo;
- una institución no puede editar roles de otra institución;
- una plantilla no se asigna directamente a usuarios;
- un rol institucional no se puede usar en otra institución;
- el último administrador institucional queda protegido;
- desactivar un rol revoca sus permisos efectivos;
- padre/responsable conserva filtro por representados;
- un rol nuevo no requiere código nuevo en el login;
- API y PostgreSQL rechazan los mismos casos de escalación;
- todos los cambios sensibles quedan auditados.

## Regla de seguridad para este bloque

No se aplicarán cambios de esquema, RLS, RPC ni asignaciones reales en Supabase durante el desarrollo. La primera entrega técnica será el inventario 042A y una migración 028 revisable acompañada de rollback, validación y pruebas.

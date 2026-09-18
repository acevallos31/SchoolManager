# 049A — Inventario de clonación del dataset Demo

## Propósito

Definir qué debe clonarse para crear una sandbox funcional sin contaminación cruzada y
sin violar unicidades globales.

Este inventario usa el esquema real vigente hasta migración 045. La implementación de
clonado se hará en 049C, no en 046.

## Regla general

Toda fila mutable que pueda editarse durante una Demo debe pertenecer exclusivamente a
esa sandbox o ser una copia propia. No compartir Personas, Usuarios ni entidades
académicas/financieras mutables entre dos sesiones.

## Orden de dependencias

| Orden | Entidad | Estrategia |
| --- | --- | --- |
| 1 | `instituciones` | crear nueva `demo_sandbox` |
| 2 | `configuracion_identificadores` | copiar hacia nueva institución |
| 3 | `ciclos_escolares` | clonar y mapear UUID |
| 4 | `periodos_matricula` | clonar con nuevo ciclo |
| 5 | `grados` / `jornadas` | clonar por institución |
| 6 | `secciones` | clonar con ciclo/grado/jornada remapeados |
| 7 | `conceptos_financieros` | clonar por institución |
| 8 | `planes_pago` | clonar por institución |
| 9 | `plan_cuotas` | clonar con plan/concepto remapeados |
| 10 | `personas` | clonar Personas sintéticas necesarias |
| 11 | `alumnos` | clonar con Persona nueva |
| 12 | `responsables` | clonar con Persona nueva |
| 13 | `alumno_responsable` | clonar relaciones remapeadas |
| 14 | `usuarios` | crear usuario del visitante, no clonar Auth de plantilla |
| 15 | rol institucional Demo | crear/clonar rol acotado |
| 16 | `usuarios_roles` | asignar solo a la sandbox |
| 17 | `matriculas` | clonar con alumno/ciclo/sección/plan remapeados |
| 18 | `cargos` | clonar con matrícula/alumno/plan/concepto remapeados |
| 19 | `pagos` | recrear/remapear con recibo nuevo |
| 20 | `pagos_aplicaciones` | clonar contra pago/cargo nuevos |

## Identificadores que requieren tratamiento especial

### Persona

`ux_personas_documento_normalizado` es global.

No copiar literalmente una identificación de la plantilla a varias sandboxes.
Opciones válidas para datos sintéticos:

- dejar documento nulo si el caso no requiere probarlo;
- generar un documento sintético único por sandbox.

### Alumno

`ux_alumnos_rne_global` es global.

- RNE sintético debe regenerarse por sandbox o quedar nulo.
- `codigo_interno` sí es único por institución y puede conservarse.

### Pago

- `numero_recibo` es global: usar la secuencia del destino.
- `referencia_externa` es única dentro de institución: puede regenerarse o quedar
  nula para evitar que el seed dependa de una referencia concreta.

## Usuario Demo

No clonar usuarios Auth de la plantilla.

049C creará/reutilizará:

1. Auth user anónimo único;
2. Persona sintética técnica para ese visitante;
3. `public.usuarios` vinculado al Auth UID;
4. rol institucional Demo acotado;
5. `usuarios_roles` limitado a la sandbox.

No se asigna `platform_admin`.

## Roles

Las plantillas globales de rol/permisos son catálogo de plataforma y no se duplican por
cada sandbox.

Debe crearse un rol institucional derivado de una plantilla autorizada o mediante el
mecanismo RBAC vigente, con permisos suficientes para demostrar:

- alumnos;
- responsables;
- matrícula;
- cargos;
- pagos;
- documentos.

Debe excluir capacidades globales, seguridad de plataforma, secretos y administración
de otras instituciones.

## Historial

`matricula_estado_historial` no debe copiarse ciegamente porque referencia usuarios.
Para el primer seed se prefiere un dataset actual coherente y pequeño; si una historia
es necesaria para demostrar un caso, debe recrearse con actores sintéticos remapeados.

`seguridad_auditoria` no se clona. La nueva sandbox genera su propia auditoría desde
cero.

## Tablas que NO se clonan

- `schema_migrations`;
- `permisos`;
- plantillas globales de `roles`;
- `auth.users` de la plantilla;
- `invitaciones_acceso`;
- `seguridad_auditoria`;
- secretos/configuración OAuth;
- datos de una institución productiva.

## Atomicidad

El clonado completo se ejecutará como una sola operación transaccional de backend/DB.
Debe mantener mapas UUID origen→destino. Si falla cualquier paso, se revierte la
sandbox completa y no se registra una `demo_session` activa.

## Pruebas que derivan de este inventario

- dos sandboxes con el mismo seed no chocan por RNE/documento;
- recibos clonados reciben números distintos;
- Personas editadas en A no cambian B;
- rol/usuario de A no tiene contexto en B;
- ninguna FK del clon apunta a la plantilla;
- ninguna fila mutable del clon conserva `institucion_id` de la plantilla.

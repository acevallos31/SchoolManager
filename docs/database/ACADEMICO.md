# Modelo académico

## Modelo elegido

Se usa un modelo directo, sin `ofertas_academicas`:

```text
Institución -> Ciclo -> Sección -> Grado
                         |
                         +-> Jornada opcional

Alumno + Sección + Periodo -> Matrícula histórica
```

Oferta académica se descartó en este bloque porque no elimina la necesidad de
proteger una matrícula por Alumno+Ciclo y agregaría una entidad sin procesos
propios. Puede reevaluarse cuando Docencia requiera asignaturas o planes por
grado/ciclo.

## Entidades

### Grado

Es catálogo global reutilizable. `nombre` es único y `orden` permite presentación
estable. No contiene secciones ni institución. Si una institución requiere una
taxonomía incompatible se evaluará una configuración adicional, no duplicación
anticipada.

### Ciclo escolar

Es una edición temporal de una Institución. Se mantiene `activo` más fechas;
todavía no existe un proceso que justifique estados planificado/abierto/cerrado.
Cerrar formalmente ciclos queda como decisión funcional futura.

### Sección

Pertenece a una Institución, Ciclo y Grado; Jornada es opcional. La FK compuesta
`(ciclo_id, institucion_id)` impide asociarla a una institución diferente de la
del ciclo.

La unicidad usa dos índices estructurales:

- con Jornada: Institución+Ciclo+Grado+Jornada+`lower(nombre)`;
- sin Jornada: Institución+Ciclo+Grado+`lower(nombre)`.

Una sección inactiva se conserva y tampoco puede duplicarse con el mismo
contexto. Para reutilizarla se reactiva el registro histórico adecuado.

### Periodo de matrícula

Pertenece a un Ciclo. Matrícula usa una FK compuesta
`(periodo_matricula_id, ciclo_id)` para impedir periodos de otro ciclo.

### Matrícula

Es un hecho histórico nuevo por cada ciclo. Contiene:

- Alumno;
- Institución y Ciclo como claves técnicas de integridad;
- Sección;
- Periodo;
- actor, fecha y estado.

`grado_id` fue eliminado y se deriva de Sección. Las FK compuestas garantizan
que Alumno, Sección, Periodo, Ciclo e Institución coincidan. El `UNIQUE` de
Alumno+Ciclo impide dos matrículas principales incluso bajo concurrencia.

## Estados

- `pendiente`: creada, aún no activada.
- `activa`: cursando actualmente.
- `finalizada`: ciclo completado; terminal.
- `retirada`: salida voluntaria/administrativa con motivo; terminal.
- `anulada`: el hecho se invalida con fecha y motivo; terminal.
- `trasladada`: continuidad fuera de esa matrícula con motivo; terminal.

Transiciones mínimas de la función:

- pendiente -> activa o anulada;
- activa -> finalizada, retirada, anulada o trasladada.

No se incluyó `cancelada` porque se solapa con `anulada` sin una definición de
negocio diferente.

## Operaciones

- `crear_seccion`: valida catálogos y contexto activo.
- `matricular_alumno`: valida contexto, periodo, cupo y duplicidad.
- `cambiar_estado_matricula`: controla transiciones mínimas y motivo.
- `desactivar_seccion`: bloquea si hay matrículas pendientes/activas.
- `crear_alumno_nueva_persona`: crea Persona+Alumno atómicamente.
- `crear_alumno_para_persona`: reutiliza una Persona activa existente.
- `desactivar_alumno` / `reactivar_alumno`: no modifican matrículas históricas.

Las funciones internas permanecen `SECURITY INVOKER` y no son ejecutables por
`authenticated`. Las RPC públicas son wrappers `SECURITY DEFINER`: resuelven el
actor con `auth.uid()`, validan permiso/Institución y reutilizan estas operaciones.

### Gestión de alumnos desde Angular

La lista y los catálogos usan PostgREST bajo RLS. Crear, desactivar y reactivar
usan RPC; Angular no ejecuta DML directo. La migración 011 agrega
`rpc_crear_alumno_nueva_persona_con_documento`, que guarda el documento civil
normalizado en `personas` y el perfil institucional en `alumnos` dentro de una
sola transacción.

La creación y la matrícula inicial permanecen separadas. Encadenar la RPC de
creación y `rpc_matricular_alumno` desde el navegador no sería atómico. El
formulario de alta no solicita ciclo, grado ni sección; tras crear ofrece
navegar al módulo de Matrículas, sin registrar matrícula automáticamente.

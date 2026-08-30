# Revisión de normalización — Fase 1C, Bloque 1

## Criterio

- **1FN:** atributos atómicos y sin grupos repetidos.
- **2FN:** cada atributo no clave depende de la clave completa.
- **3FN:** ningún atributo no clave depende transitivamente de otro atributo no
  clave. Los valores derivables se resuelven por relaciones, no se copian.

## Revisión del esquema actual

| Tabla | PK y FK principales | Dependencias y hallazgos |
| --- | --- | --- |
| `personas` | PK `id` | Nombres y contacto dependen de la Persona. La identificación normalizada es una representación técnica del documento, protegida por coherencia y unicidad. `pais_emisor` nullable queda pendiente según configuración documental. |
| `usuarios` | PK `id`; FK `persona_id` | `auth_user_id` y `persona_id` son únicos. `rol` viola el requisito multirrol y se mantiene solo como puente temporal; RBAC pasa a `usuarios_roles`. Fecha/motivo de desactivación requieren una regla de coherencia futura. |
| `alumnos` | PK `id`; FK Persona e Institución | Correcto como perfil institucional. `rne` global único es una decisión pendiente; `fecha_nacimiento` podría pertenecer a Persona si todos los perfiles comparten el mismo dato. |
| `ciclos_escolares` | PK `id`; FK Institución | Nombre es único por institución y las fechas son coherentes. `activo` no expresa por sí solo estados como planificado/cerrado; se revisará en Bloque 2. |
| `grados` | PK `id` | Catálogo global normalizado. Falta decidir si nombres/orden son globales o configurables por institución. |
| `jornadas` | PK `id` | Catálogo global. Misma decisión institucional pendiente que Grados. |
| `secciones` | PK `id`; FK Grado y Jornada | Falta Ciclo e Institución. Su unicidad actual hace que una sección sea fija al grado y no histórica por ciclo. Es el hallazgo crítico para Bloque 2. |
| `responsables` | PK `id`; FK Persona e Institución | Perfil institucional normalizado; la unicidad Persona+Institución evita duplicidad. |
| `alumno_responsable` | PK `id`; FK Alumno y Responsable | N:M correcta; parentesco y accesos dependen del vínculo. La unicidad permanente impide recrear un vínculo después de desactivarlo; debe decidirse si se reactiva el mismo registro. |
| `periodos_matricula` | PK `id`; FK Ciclo | Correctamente dependiente del ciclo. Falta validar que sus fechas estén dentro del ciclo y catalogar `tipo`. |
| `matriculas` | PK `id`; FK Alumno, Ciclo, Grado, Sección, Periodo, Usuario | `ciclo_id` y `grado_id` serán derivables cuando Sección dependa de Ciclo y Grado; hoy no hay constraint que garantice coincidencia. `periodo_matricula_id` también determina Ciclo. Es una violación 3FN y riesgo de combinaciones contradictorias. No se modifica en este bloque. |

## RBAC normalizado

- `roles.codigo -> nombre, descripción, flags`: catálogo sin usar el nombre como
  clave lógica.
- `permisos.codigo -> módulo, nombre, descripción`: el código es único y el
  módulo debe coincidir con su primer segmento.
- `usuarios_roles`: los atributos de vigencia dependen de una asignación entre
  Usuario, Rol y ámbito institucional.
- `roles_permisos`: relación N:M pura con PK compuesta.

No se almacenan listas de roles o permisos dentro de `usuarios`; se obtienen por
joins. `UsuarioActual.Roles` y `Permisos` serán proyecciones, no columnas.

## Riesgos históricos y nullable

Las FK actuales no declaran acciones y PostgreSQL aplica `NO ACTION`, equivalente
a restricción al finalizar la sentencia en estos casos. En Bloque 2 se nombrarán
y documentarán explícitamente las FK históricas. No deben borrarse físicamente
alumnos, ciclos, secciones, matrículas ni asignaciones de rol con uso histórico.

Los nullable de transición en migraciones 003–006 permiten instalar sobre el
legacy, mientras el baseline nuevo usa `NOT NULL` donde ya existe información
canónica. Antes de endurecer una columna incremental se requiere backfill y una
validación sin filas pendientes.

## Decisiones del Bloque 2

- Sección depende de Institución + Ciclo + Grado y opcionalmente Jornada.
- Dos índices parciales resuelven la unicidad estructural cuando Jornada es
  nullable, incluyendo `lower(nombre)`.
- Matrícula elimina `grado_id`; el grado se deriva exclusivamente de Sección.
- Matrícula conserva `ciclo_id` e `institucion_id` como claves técnicas. Son una
  excepción documentada a 3FN para habilitar FK compuestas y la restricción
  concurrente `UNIQUE (alumno_id, ciclo_id)` sin lógica procedural.
- Las FK compuestas impiden mezclar Alumno, Sección y Periodo de contextos
  distintos.
- No se introduce `ofertas_academicas`: añadirla no elimina la necesidad de una
  clave de ciclo en Matrícula y agrega una entidad sin comportamiento actual.
- Los estados son un conjunto cerrado y cada cambio genera histórico.
- Las columnas legacy `alumnos.nombres`, `apellidos` y `dni` se conservan solo en
  instalaciones incrementales, nullable y sin recibir datos nuevos. Persona es
  la fuente canónica; el baseline limpio no las contiene.

La excepción técnica está acotada por constraints: los valores redundantes no
pueden divergir de sus determinantes. No se conserva `grado_id` porque no aporta
una garantía que no cubra la FK a Sección.

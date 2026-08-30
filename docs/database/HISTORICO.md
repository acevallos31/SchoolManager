# Histórico y retención académica

## Información conservada

- Una Matrícula por cada Alumno y Ciclo.
- La Sección, Grado, Periodo, Ciclo e Institución usados en ese hecho.
- Estado inicial y cada cambio posterior.
- Usuario y motivo cuando el cambio usa las funciones académicas.
- Fechas y motivos de desactivación de Alumnos y Secciones.

`matricula_estado_historial` recibe el estado inicial en INSERT y cambios reales
de estado en UPDATE. El trigger no matricula, no valida permisos y no ejecuta
procesos académicos; se limita a auditoría automática.

## Retención y DELETE

Las FK académicas relevantes usan `ON DELETE RESTRICT`:

| Relación | Política | Motivo |
| --- | --- | --- |
| Alumno -> Persona/Institución | RESTRICT | conservar identidad del perfil |
| Sección -> Ciclo/Grado/Jornada | RESTRICT | conservar contexto académico |
| Matrícula -> Alumno/Sección/Periodo/Usuario | RESTRICT | preservar el hecho y actor |
| Historial -> Matrícula/Usuario | RESTRICT | preservar trazabilidad |

No se elimina físicamente un Alumno, Ciclo, Sección, Periodo o Matrícula usado.
La aplicación debe desactivar o cambiar estado mediante operaciones explícitas.

## Desactivación

Desactivar no equivale a `DELETE CASCADE`:

- Alumno con matrícula pendiente o activa: operación bloqueada.
- Alumno con matrículas terminales: puede desactivarse; estas no cambian.
- Reactivar Alumno: limpia datos de desactivación, sin reactivar matrículas.
- Sección con matrículas pendientes o activas: operación bloqueada.
- Sección sin procesos vigentes: se marca inactiva con fecha y motivo.

No se retiran alumnos ni se cambian estados silenciosamente. Las reglas de cierre
de ciclo y traslado masivo siguen pendientes de definición funcional.

## Rollback

El rollback 008 es deliberadamente conservador: se bloquea si existen Secciones,
Matrículas, historial o datos nuevos que perderían contexto. No colapsa secciones
de ciclos distintos ni elimina trazabilidad para recuperar el esquema anterior.

`authenticated` solo puede consultar historial cuando la policy permite ver la
Matrícula. No tiene privilegios INSERT, UPDATE ni DELETE; el trigger es el único
escritor desde las operaciones aprobadas.

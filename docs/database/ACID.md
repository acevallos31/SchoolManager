# Garantías ACID y concurrencia

ACID es provisto por PostgreSQL y reforzado por el diseño; no nace simplemente
por usar funciones.

## Atomicity

Cada llamada SQL a una función se ejecuta dentro de la transacción PostgreSQL.
`crear_alumno_nueva_persona` inserta Persona y Alumno en una unidad: si falla la
segunda inserción, la Persona creada por esa llamada se revierte. Las funciones
no ejecutan `COMMIT` interno.

## Consistency

- PK UUID y `NOT NULL` para identidades y contexto obligatorio.
- FK compuestas para Institución+Ciclo, Alumno+Institución,
  Sección+Ciclo+Institución y Periodo+Ciclo.
- `UNIQUE (alumno_id, ciclo_id)` para una matrícula por ciclo.
- Índices únicos de Sección que resuelven Jornada nullable.
- CHECK de estados, cupo, textos y coherencia de anulación.
- FK `RESTRICT` para retención histórica.

## Isolation

PostgreSQL MVCC mantiene aislamiento normal entre operaciones. Matricular toma
`SELECT ... FOR UPDATE` sobre la fila de Sección antes de contar cupo. Dos
solicitudes para la misma sección se serializan: la segunda observa la matrícula
confirmada por la primera y no excede el cupo.

El lock protege el contador de cupo. El constraint único protege una carrera
distinta: dos secciones del mismo ciclo para el mismo alumno. Se necesitan ambos
porque bloquean invariantes diferentes. No se usan advisory locks ni un nivel de
aislamiento global más costoso.

## Durability

Una transacción confirmada queda bajo WAL y persistencia de PostgreSQL/Supabase.
La aplicación no mantiene una segunda fuente de verdad para matrículas o estados.

## Fallos parciales

- Una matrícula rechazada no crea Matrícula ni historial.
- Un Alumno inválido no deja una Persona huérfana creada por la misma función.
- Un cambio de estado inválido no altera Matrícula ni historial.

## Frontera de seguridad

RLS controla lecturas simples. Las mutaciones críticas no tienen grants directos
para `authenticated`; las RPC seguras validan identidad, permiso e institución
antes de entrar a las mismas funciones transaccionales. Esto evita que un cliente
salte constraints de proceso mediante PostgREST directo.

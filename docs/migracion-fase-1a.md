# Migracion Fase 1A

## Regla operativa

Los archivos de `database/migrations` son artefactos preparados y no deben
ejecutarse todavia. No contienen `DROP`, no eliminan datos ni cambian contratos
de backend o frontend.

## Orden futuro

1. Convencion y registro de migraciones.
2. Instituciones y configuracion de identificadores.
3. Personas y extension anulable de alumnos y usuarios.
4. Responsables y relacion alumno-responsable.
5. Ciclos y periodos de matricula.
6. `006_extender_matriculas_contexto_canonico`: periodo y trazabilidad de anulacion.

La oferta academica, secciones por ciclo, cupo y jornada por ciclo se conservan
en `database/migrations/future/` como evolucion futura y no son ejecutables en
Fase 1A.

La secuencia activa exacta es `001`, `002`, `003`, `004`, `005` y `006`.
No incluye migraciones financieras, RLS, autorizacion, oferta academica ni
secciones por ciclo.

## Backfill propuesto

El backfill se ejecutara solo tras aprobacion separada. Creara una persona por
alumno verificable, asociara usuarios a una unica persona y migrara `tutor_id`
solo cuando apunte a un usuario comprobable. El texto `padres_encargados` se
incluira en un reporte de conciliacion, sin conversion automatica.

Los ciclos, grados y secciones se asociaran solamente cuando exista una
correspondencia inequivoca. Las filas ambiguas quedaran pendientes y no recibirán
valores inventados.

## Conciliacion

Antes de activar restricciones definitivas se revisaran alumnos sin persona,
usuarios sin persona, documentos duplicados tras normalizacion, tutores no
verificables, responsables duplicados, alumnos con mas de un principal activo,
ciclos sin institucion y matriculas sin contexto academico verificable.

## Rollback

Cada migracion tiene un rollback separado. Los rollbacks eliminan solo objetos
nuevos que no tengan dependencias y columnas nuevas anulables; nunca eliminan
datos legacy. Antes de una ejecucion se requiere respaldo logico, conteos base y
registro de lote para el backfill futuro.

## Exclusiones de Fase 1A

No se crean obligaciones financieras ni aplicaciones de pago. No se modifica
`cargos`, `pagos`, su comportamiento, sus saldos ni sus estados. RLS se documenta
pero no se activa ni endurece hasta Fase 1B.

## Estrategia de pruebas

Fase 1A requiere pruebas de integracion sobre PostgreSQL aislado. Verifican PK
UUID, FK, checks, indices unicos parciales, idempotencia de migraciones y
proteccion de rollbacks. Las consultas de validacion existentes siguen siendo
diagnosticos de conciliacion y no pruebas de comportamiento de casos de uso.

Fase 1B agrega pruebas unitarias de los casos de uso de personas, responsables,
autorizacion y matricula. Fase 2 agrega pruebas ACID financieras.

## Regla RNE

El RNE es nullable y unico global cuando existe. La migracion `003` crea el
indice parcial correspondiente con comparacion exacta. La definicion formal de
formato y normalizacion queda pendiente; Fase 1A no aplica `lower`, trim, regex
ni reglas nacionales.

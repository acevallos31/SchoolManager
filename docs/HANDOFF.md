# HANDOFF — Producción alineada hasta migración 026

## Fecha
2026-09-09 (UTC-6).

## Rama de documentación
`docs/cierre-prod-019-025`

## Estado general
Producción quedó alineada con el esquema esperado por `main` hasta la migración 026.
El incidente de permisos/carga de `/configuracion/estructura-academica` quedó resuelto.
La rematrícula tras anulación quedó corregida y validada funcionalmente en producción.

## Qué ocurrió
El frontend/backend ya consumían contratos y permisos introducidos después de la migración 018, pero la base de datos de producción seguía detenida en 018.

Esto provocaba en Estructura Académica:
- `No tienes permiso para realizar esta operación`;
- listado de grados vacío;
- comportamiento inconsistente entre código desplegado y esquema real.

Posteriormente se detectó otro problema funcional en Matrículas: una fila anulada seguía ocupando la unicidad global `(alumno_id, ciclo_id)` y bloqueaba una nueva matrícula en el mismo ciclo.

## Auditoría previa
Se confirmó en producción que:
- `schema_migrations` llegaba únicamente hasta 018;
- 019→025 no estaban registradas;
- tampoco existían físicamente sus objetos principales;
- el gate de datos de 020 no tenía conflictos:
  - 1 institución activa;
  - 0 grados huérfanos;
  - 0 jornadas huérfanas;
  - 0 grados compartidos;
  - 0 jornadas compartidas;
  - 0 duplicados por institución.

## Backup
Antes de aplicar 019→025 se generó backup restorable del schema `public` con PostgreSQL 17 `pg_dump`.
No restaurar sobre producción salvo incidente real y procedimiento explícito.

## PR #60 — validaciones de migraciones
Mergeado en `main`:
- merge SHA: `0ede6ff9a5d9f9ff69da3f1e0a693ce89c304d59`;
- añade validaciones faltantes de 019 y 022;
- fortalece `MigrationTests` para exigir relación 1:1 migración/rollback/validation;
- CI run #287: verde;
- Sonar Quality Gate: verde;
- no se bajaron thresholds ni se excluyeron validaciones.

## Migraciones aplicadas a producción
Se ejecutaron 019→025 una por una con PostgreSQL 17 `psql`, `ON_ERROR_STOP=1`, deteniéndose después de cada una para validar:

1. `019_cargos_mensualidades_obligaciones.sql` — COMMIT — validación: 0 hallazgos.
2. `020_grados_jornadas_multiinstitucion.sql` — COMMIT — validación: 0 hallazgos.
3. `021_pagos_cobranza.sql` — COMMIT — validación: 0 hallazgos.
4. `022_portal_responsable_lectura.sql` — COMMIT — validación: 0 hallazgos.
5. `023_rbac_permisos_aplicacion_ciclos.sql` — COMMIT — validación: 0 hallazgos.
6. `024_rbac_permisos_aplicacion_estructura.sql` — COMMIT — validación: 0 hallazgos.
7. `025_fix_unicidad_grados_jornadas_institucion.sql` — COMMIT — validación: 0 hallazgos.

Después del merge del PR #63 se aplicó:

8. `026_permitir_rematricula_tras_anulacion.sql` — aplicada — validación: 0 hallazgos.

## PR #62 — navegación y UX
Mergeado en `main`:
- merge SHA: `d08235132b4c3c0f8b84d3bd8f8560bdbd49fdf5`;
- Ciclos Escolares y Estructura Académica visibles desde AppShell según permisos;
- Cargos/Pagos con selector de alumno y preservación de `?alumnoId=...`;
- fixes zoneless en Cargos, Pagos y Matrículas;
- errores de Matrículas dentro del formulario/modal activo;
- validación manual confirmó que los alumnos cargan de forma consistente en los selectores;
- CI y Sonar Quality Gate verdes antes del merge.

## PR #63 — rematrícula tras anulación
Mergeado en `main`:
- merge SHA: `e51188b032c31a8f8dcff42a32e7d3b4b4b06893`;
- migración 026;
- reemplaza la restricción UNIQUE global de matrícula alumno+ciclo por índice UNIQUE parcial con el mismo nombre `uq_matriculas_alumno_ciclo`;
- predicado: `estado <> 'anulada'`;
- una matrícula anulada libera alumno+ciclo para una nueva matrícula;
- una matrícula no anulada sigue bloqueando duplicados;
- añade `ix_matriculas_alumno` para historial completo;
- rollback seguro: aborta si revertir implicaría perder consistencia;
- validation 026 + tests específicos;
- CI run #317 completamente verde, incluidos tests DB y Sonar Quality Gate.

## Resultado funcional
Después de refrescar sesión/login:
- `/configuracion/estructura-academica` carga correctamente;
- desapareció el mensaje de falta de permiso;
- los datos académicos vuelven a mostrarse;
- no se observaron errores visibles en la prueba manual.

Pruebas manuales de 026 en producción:
- matrícula previa `anulada` -> nueva matrícula en el mismo ciclo: permitida;
- matrícula `activa` -> nueva matrícula en el mismo ciclo: bloqueada;
- matrícula `finalizada` -> nueva matrícula en el mismo ciclo: bloqueada.

La regla queda: solo `anulada` libera alumno+ciclo; `pendiente`, `activa`, `finalizada`, `retirada` y `trasladada` siguen protegidas por la unicidad parcial.

## Estado de producción
```text
001-018  ya aplicadas previamente
019      aplicada + validada
020      aplicada + validada
021      aplicada + validada
022      aplicada + validada
023      aplicada + validada
024      aplicada + validada
025      aplicada + validada
026      aplicada + validada
```

## Puntos importantes
- 025 corrige el residual histórico de unicidad global de grados/jornadas que 020 no cubría para todos los nombres de constraints legacy.
- La unicidad final de grados/jornadas es por institución con índices:
  - `ux_grados_institucion_nombre`;
  - `ux_jornadas_institucion_nombre`;
  ambos `UNIQUE` sobre `(institucion_id, lower(btrim(nombre)))`.
- 023/024 agregan permisos de aplicación para ciclos y estructura.
- 026 conserva historial de matrícula y cambia únicamente la regla de unicidad para el estado `anulada`.
- La capa interna DB conserva permisos `configuracion.*`; no mezclar con namespaces de aplicación sin una decisión explícita de arquitectura.

## Arquitectura vigente
- Angular -> API .NET -> PostgreSQL/Supabase/RPC.
- Supabase directo en frontend únicamente para Auth (`auth.ts`).
- Deuda #10 de Supabase directo de negocio: resuelta en Bloque 030 / PR #53.
- `PermissionGuard` es el guard vigente.
- `AdminGuard`/`PadreGuard` no deben reintroducirse.
- `/portal-padre` sigue fuera del AppShell administrativo y es read-only.

## Deuda técnica pendiente
- Mensajes de error de negocio amigables: algunos conflictos `23505` todavía muestran texto crudo de PostgreSQL en UI.
- Auditoría de issues `High` del Overall Code en Sonar.
- E2E autenticado completo en staging seguro.
- Selector global multiinstitución.
- Revisión futura de divergencia de namespaces de permisos app/DB.
- Observabilidad adicional si se necesita trazabilidad más allá de `/health` y `/health/ready`.

## Próximos pasos
1. Mejorar mapeo de errores de negocio sin debilitar las invariantes DB.
2. Auditar issues `High` históricos de Sonar.
3. Preparar/ejecutar E2E autenticado en staging seguro.
4. Más adelante: selector global multiinstitución y observabilidad adicional.

## Reglas operativas
- Siempre rama; no escribir directo a `main`.
- No force push.
- No secretos en repo.
- No E2E destructivo en producción.
- No aplicar migraciones fuera de orden.
- Validar cada migración antes de continuar.
- `AGENTS.md` sigue siendo la referencia operativa del repositorio.

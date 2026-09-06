# Handoff — Deuda técnica #2: grados/jornadas multiinstitución

**Rama:** `chore/deuda-2-grados-jornadas-multiinst`
**Migración:** `database/migrations/020_grados_jornadas_multiinstitucion.sql`
**Estado:** Implementada, corregida y validada — **sin merge** (PR abierto a revisión).

---

## 1. Root cause

`grados` y `jornadas` eran catálogos **globales** (sin `institucion_id`), mientras
que `secciones`, `ciclos_escolares`, `matriculas`, `conceptos`, `planes` y
`responsables` ya eran por-institución.

Dos vectores de fuga de aislamiento multiinstitución:

1. **RPC de configuración** (`rpc_listar/crear/actualizar/cambiar_estado_grado*`
   y `*_jornada*`, de la fase 016) validaban únicamente el permiso del rol, pero
   sus `SELECT`/`UPDATE` **no filtraban por institución**.
2. **RLS de lectura** de `grados`/`jornadas` usaba `usuario_tiene_permiso_en_algun_ambito`
   → una institución autorizada en *cualquier* ámbito podía leer el catálogo de
   *todas*.

**Resultado:** una institución autorizada veía y podía modificar los catálogos
de configuración académica de las demás. El backend productivo **no** inserta
grados/jornadas (0 matches en `*.cs`); los cambios se concentran en SQL (RPCs/RLS).

## 2. Decisión de modelo — `institucion_id` directo

Se adopta `institucion_id` **directo** como columna FK en `grados` y `jornadas`,
siguiendo el patrón canónico por-institución ya usado por `secciones`,
`ciclos_escolares`, `conceptos`, `planes` y `responsables`. **No** se usó tabla
puente intermedia (no aporta nada: la relación es 1 grado/jornada → 1 institución,
y complica unicidad, RLS y RPC sin beneficio).

La cláusula de parada («si `institucion_id` directo rompiera una invariancia»)
**no se activó**: el modelo directo es consistente con el resto del esquema.

## 3. Estrategia de backfill

Determinista, en dos modos según el estado de los datos:

- **Monoinstitucional** (institución activa única): todo grado/jornada se asigna
  a esa institución.
- **Multiinstitucional**: la institución se infiere por referencia **inequívoca**
  desde `secciones` (join por `grado_id`/`jornada_id`).

`NOT NULL` se declara **solo después** del backfill.

### Por qué aborta ante registros compartidos/huérfanos

Un grado/jornada que esté referenciado por `secciones` de **varias**
instituciones distintas, o que **no** sea referenciado por ninguna (huérfano, en
modo multiinstitucional), no admite asignación inequívoca. En ese caso la
migración **aborta con error explícito** en lugar de duplicar el registro.

**No se duplica silenciosamente información** porque duplicar cambiaría la
identidad funcional (el grado/jornada pasaría a ser dos entidades distintas
gestionadas por separado). Abortar fuerza una decisión explícita y conservadora
antes de migrar.

## 4. Invariantes nuevas tras 020

- `grados.institucion_id` / `jornadas.institucion_id` **NOT NULL** con FK a
  `instituciones(id)`.
- Unicidad de nombre **por institución**: `ux_grados_institucion_nombre` /
  `ux_jornadas_institucion_nombre` sobre `(institucion_id, lower(btrim(nombre)))`.
  Se retira la unicidad global pre-020 (`grados_nombre_key` / `jornadas_nombre_key`).
- **Mismo nombre permitido entre instituciones, pero no dentro de la misma.**

## 5. FKs compuestas de `secciones`

Se añaden FKs compuestas en `secciones`:

- `(grado_id, institucion_id)` → `grados(id, institucion_id)`
- `(jornada_id, institucion_id)` → `jornadas(id, institucion_id)`

Esto garantiza a nivel de constraint que una sección solo puede referenciar un
grado/jornada **de su misma institución** (no basta con que ambos IDs existan por
separado). Requirió índices únicos `(id, institucion_id)` en grados/jornadas como
objetivo de la FK compuesta. La FK simple pre-020 de `secciones.grado_id`/`jornada_id`
se sustituye por la compuesta.

## 6. RLS y RPC modificadas

- **RLS de lectura** de `grados`/`jornadas`: de `usuario_tiene_permiso_en_algun_ambito`
  a filtro por fila con `usuario_tiene_permiso_actual(<permiso>, institucion_id)`.
- **RPC** (`rpc_listar_grados`, `rpc_crear_grado`, `rpc_actualizar_grado`,
  `rpc_cambiar_estado_grado`, y sus equivalentes de jornadas):
  - Filtran `SELECT`/`UPDATE` por `institucion_id`, con el contexto resuelto vía
    `resolver_institucion_operacion` (el `p_institucion_id` debe coincidir con el
    contexto autorizado del rol).
  - Devuelven la columna `institucion_id`.
  - Cambio de firma de retorno → `drop function if exists …` previo a la recreación.
- Se mantiene `SECURITY DEFINER` con `search_path` explícito (sin regresión de
  seguridad).

## 7. Grants

- `REVOKE … FROM public` de las firmas previas y `GRANT EXECUTE … TO authenticated`
  **sobre todas las firmas nuevas**, incluidos los `rpc_cambiar_estado_*`
  (`rpc_cambiar_estado_grado(uuid, boolean, uuid)` y
  `rpc_cambiar_estado_jornada(uuid, boolean, uuid)`), que inicialmente faltaban
  (ver defectos). Idem en el rollback para restaurar el estado original.

## 8. Defectos reales encontrados durante las pruebas

La suite DB (1.ª corrida: **124 passed / 7 failed**) reveló defectos reales en el
primer borrador de 020, que se corrigieron **sin rediseño** (la orden lo permite
cuando una prueba demuestra un defecto):

1. **Constraint global no dropeado.** 020 intentaba dropear
   `ux_grados_nombre_normalizado` / `ux_jornadas_nombre_normalizado` (inexistentes).
   El UNIQUE global real de la baseline era `grados_nombre_key` / `jornadas_nombre_key`
   (auto-nombrado por `unique(nombre)`). Síntoma: **23505** al crear "mismo nombre
   en instituciones A y B". Corregido en migración + rollback + validation.
2. **Grants `rpc_cambiar_estado_*` ausentes.** Tras el `REVOKE … FROM public`, no
   se re-granted a `authenticated`. Síntoma: **42501 permission denied**. Corregido
   en migración + rollback.
3. **Validation SQL:** alias `esperado` duplicado (dos subconsultas en el chequeo
   de grants anon/public) → renombrado a `roles`.
4. **`MigrationTests`** esperaba la lista activa hasta `019`; ampliada a incluir
   `020_grados_jornadas_multiinstitucion.sql`.

Se eliminó **una** aserción semánticamente errónea en los tests nuevos (listar con
contexto institucional ajeno esperando filas): no se debilitaron asserts válidos.

## 9. Pruebas multitenancy

Nuevo `GradosJornadasMultitenancyTests.cs` (**424 líneas, 16 tests**), reutilizando
el harness de autenticación de `CargosMultitenancyTests`
(`SetAuthenticatedAsync`, `AuthScalarAsync`, `InsertInstitucionConAdminAsync`,
`ResetAsync`) — no se inventó otro harness. Cubre:

1. Institución A lista solo sus grados.
2. Institución A lista solo sus jornadas.
3. A no obtiene por UUID un grado de B.
4. A no obtiene por UUID una jornada de B.
5. A no actualiza un grado de B.
6. A no actualiza una jornada de B.
7. A no cambia el estado de grado/jornada de B.
8. Mismo nombre de grado permitido en A y B.
9. Mismo nombre de jornada permitido en A y B.
10. Duplicado dentro de la misma institución rechazado.
11. Sección no puede combinar `grado_id` de A con `institucion_id` B.
12. Sección no puede combinar `jornada_id` de A con `institucion_id` B.
13. Usuario multiinstitución ve solo el contexto pasado/autorizado.
14. UUID conocido de B no permite bypass de contexto.
15. RPC crear/actualizar/listar/cambiar_estado respetan `institucion_id`.
16. (setup/rollback) `ResetAsync` / `multiples_instituciones` consistente con el harness.

### Tests/fixtures adaptados a NOT NULL

`InsertGradoAsync` / `CrearGradoAsync` y equivalentes ahora reciben
**explícitamente** `institucionId` (sin institución global por defecto oculta),
propagando el contexto en cada test:

- `AcademicModelTests.cs`, `SchemaConstraintsTests.cs`, `CargosMultitenancyTests.cs`,
  `MigrationTests.cs` (lista 001–020), `AcademicStructureConfigurationTests.cs`,
  `ResponsablesGestionTests.cs`, `RlsSecurityTests.cs`.
- Factories de API: `CargosApiFactory.cs`, `MatriculasApiFactory.cs`
  (`CrearGradoAsync(Guid institucion)`).
- `perf/benchmark/seed.sql` (2 inserts de grados con `institucion_id = v_inst`).

## 10. Resultados exactos de validación

| Gate | Resultado |
|---|---|
| DB integration | **131/131 PASS** (incluye 16 multitenancy + validation suite + round-trip rollback) |
| API integration | **71/71 PASS** |
| `dotnet build -c Release` | **0 warnings / 0 errors** |
| Frontend tests | **141/141 PASS** (23 files) |
| Frontend production build | OK |
| `git diff --check` | clean |
| `hermes-control.yml` | intacto (sin cambios) |

El `PostgreSqlFixture` arranca su **propio Testcontainers postgres** y aplica las
migraciones 001–020 en fresco cada corrida → checksums de `MigrationRunner.cs`
validados contra DB limpia. El round-trip rollback→reapply queda ejercitado por
`RollbackTests` (revert completo + reapply completo).

## 11. Riesgos / legacy conservado (intencional)

Conservan catálogo global **a propósito** (documentado también en
`docs/technical-debt.md` §2):

- **Seed de la baseline `001`**: absorbido por el backfill al migrar (monoinstitución).
- **Migraciones históricas `008` / `016`**: no se reescriben.
- **`rollback/020`**: restaura el modelo global (grados/jornadas sin
  `institucion_id`, unicidad global por nombre, RLS `en_algun_ambito`, FK simple
  de `secciones`).

**Legacy que NO se conserva**: catálogo global en código/tests productivo. Todos
los inserts de grados/jornadas (tests, factories, benchmark) llevan
`institucion_id`; el backend productivo no inserta estos catálogos.

## 12. Siguiente bloque recomendado

**Bloque 021 (pagos / obligaciones / `portal-padre` / Vertic)** — fuera de alcance
de este PR a propósito. Consume el esquema de pagos existente. Pendiente conocido:
`portal-padre.ts` referencia un esquema de pagos inexistente → resolver cuando se
aborde 021. No se tocó `SONAR_TOKEN`, ni Vertic, ni producción/Supabase remoto.

---

**No mergear.** PR abierto para revisión humana.

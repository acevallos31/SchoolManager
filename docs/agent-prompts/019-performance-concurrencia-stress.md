# Bloque PERF-01 — Rendimiento, concurrencia, idempotencia y stress testing

## Objetivo

Fortalecer SchoolManager antes de continuar con más módulos funcionales. El foco es rendimiento real con datos representativos, consistencia transaccional, concurrencia segura e idempotencia.

No iniciar Responsables/finanzas durante este bloque salvo que sea estrictamente necesario para corregir una dependencia común.

## Gate 0

1. Leer completos `AGENTS.md`, `docs/AI_CONTEXT.md`, `README.md` y el HANDOFF vigente.
2. Actualizar `main` con `git fetch`, `git checkout main`, `git pull --ff-only`.
3. Verificar que el PR #29 esté mergeado en `main` y que el working tree esté limpio.
4. Crear rama `perf/escalabilidad-concurrencia-stress`.
5. No trabajar si el PR #29 no está mergeado.

## Reglas arquitectónicas obligatorias

- PostgreSQL/Supabase es la fuente de verdad.
- El caché NUNCA decide integridad, cupo, matrícula, saldo, pago, cargo o estado transaccional.
- Toda escritura crítica debe validarse dentro de la misma transacción que persiste el cambio.
- Preservar ACID.
- Aplicar SOLID de forma pragmática sin introducir CQRS, MediatR, microservicios, Generic Repository o UnitOfWork artificial.
- No aplicar migraciones ni escribir datos en Supabase remoto.
- Cualquier benchmark con volumen debe ejecutarse en Testcontainers/local o entorno desechable.

## PERF-01 — Medición base

Crear un benchmark reproducible con datos sintéticos locales:

- 2,000 alumnos
- al menos 5,000 matrículas históricas
- 30–50 secciones
- varios ciclos y períodos

Medir antes de optimizar:

- tiempo de listar alumnos
- tiempo de abrir `/matriculas`
- listado de matrículas
- filtros por alumno/ciclo/estado
- consultas de catálogos
- p50, p95 y p99 cuando aplique

Documentar baseline.

## PERF-02 — Paginación y búsqueda server-side

Implementar paginación real para listados grandes.

### Alumnos

Evitar descargar todos los alumnos para mostrar una página.

Soportar conceptualmente:

- `page`
- `pageSize`
- búsqueda por nombre/identidad/RNE
- estado

El backend/DB debe filtrar antes de devolver resultados.

### Matrículas

Agregar filtros server-side:

- alumnoId
- cicloId
- estado
- institución/contexto actual
- page
- pageSize

No descargar todas las matrículas y filtrarlas únicamente en Angular.

Mantener contratos compatibles cuando sea razonable.

## PERF-03 — Consultas e índices

Usar `EXPLAIN (ANALYZE, BUFFERS)` únicamente en DB local/Testcontainers para consultas críticas.

Agregar índices solo cuando exista evidencia de que mejoran una consulta real.

No crear índices especulativos.

Revisar especialmente:

- `alumnos`
- `matriculas`
- `periodos_matricula`
- `secciones`
- joins por Persona/Institución/Ciclo
- filtros estado/ciclo/alumno

Si hace falta migración nueva, crearla secuencialmente con validation/rollback según convención del repo, pero NO aplicarla en Supabase remoto.

## PERF-04 — Caché selectivo

Evaluar caché solo después de optimizar consultas y paginación.

Candidatos razonables:

- grados
- jornadas
- instituciones
- ciclos activos
- períodos
- otros catálogos de baja mutación

No cachear como autoridad:

- cupo disponible
- matrícula existente
- estado vigente de matrícula
- saldo
- pago
- cargo
- mensualidad pendiente

Si una aplicación puede escalar a múltiples instancias, preferir una abstracción compatible con cache distribuido. No añadir Redis si no hay beneficio medido; documentar decisión.

Definir TTL e invalidación explícitos si se implementa caché.

## PERF-05 — Idempotencia

Auditar POST críticos y diseñar protección contra:

- doble clic
- reintentos de red
- timeouts con retry
- requests duplicados

Para Matrículas, conservar la restricción alumno+ciclo como defensa de integridad y evaluar idempotency-key explícita en API.

Si se implementa idempotencia:

- mismo request + misma key => mismo resultado lógico
- no duplicar registros
- respuesta segura bajo concurrencia
- persistencia de idempotencia atómica con la operación

No usar memoria local como única garantía de idempotencia.

## PERF-06 — Concurrencia

Agregar pruebas concurrentes reales contra PostgreSQL local.

Caso obligatorio:

Una sección con 1 cupo restante y múltiples solicitudes simultáneas.

Resultado esperado:

- exactamente una creación válida
- el resto rechazadas de forma controlada
- nunca exceder cupo
- nunca crear duplicados

Agregar también prueba de múltiples requests concurrentes para el mismo alumno/ciclo.

Revisar bloqueos `FOR UPDATE`, restricciones únicas y niveles de aislamiento. Cambiar solo con evidencia.

## PERF-07 — Stress testing

Agregar k6 u otra herramienta ligera y reproducible al repo sin secretos.

Escenarios mínimos:

- login/auth mockeado o token de entorno no versionado cuando aplique
- listar alumnos paginados
- buscar alumno
- listar matrículas
- crear matrícula en entorno local/desplegable seguro

Ejecutar escalones aproximados:

- 50 usuarios concurrentes
- 100
- 250
- 500 si el entorno local lo soporta sin falsear resultados

Capturar:

- throughput
- p50
- p95
- p99
- tasa de errores
- CPU/RAM cuando sea accesible
- conexiones PostgreSQL
- locks/timeouts

No afirmar capacidad de producción solo a partir de laptop/Testcontainers. Documentar límites del entorno.

## PERF-08 — Connection pooling

Revisar `NpgsqlDataSource` y configuración de pooling.

No abrir conexiones persistentemente por request más tiempo del necesario.

Verificar que no exista patrón N usuarios = N conexiones permanentes.

Documentar parámetros recomendados, pero no cambiar producción sin evidencia y autorización.

## Calidad y seguridad

Ejecutar al final:

```bash
dotnet build backend/SchoolManager.API/SchoolManager.API.csproj -c Release
dotnet test tests/SchoolManager.API.IntegrationTests/SchoolManager.API.IntegrationTests.csproj -c Release
dotnet test tests/SchoolManager.Database.IntegrationTests/SchoolManager.Database.IntegrationTests.csproj -c Release
cd frontend/schoolmanager-frontend
npm ci --ignore-scripts
npx ng test --watch=false
npm run build -- --configuration production
cd ../..
git diff --check
```

No exponer secretos ni datos reales.

## Autonomía

Autorizado:

- modificar código dentro del bloque
- agregar tests
- agregar herramientas de benchmark/stress sin secretos
- crear migración nueva si la evidencia lo requiere
- commit
- push normal
- crear PR
- actualizar documentación y HANDOFF
- marcar PR Ready for review si todos los checks quedan verdes

No autorizado:

- merge a main
- force push
- `git reset --hard`
- `git clean -fd`
- escribir datos en Supabase remoto
- aplicar migraciones remotas
- cambiar infraestructura productiva
- introducir Redis solo para "cumplir" el prompt sin beneficio medido

## Criterio de cierre

El bloque se considera terminado cuando exista evidencia reproducible de:

- paginación server-side funcional
- búsqueda server-side en listados grandes
- tiempos base y tiempos posteriores comparados
- pruebas concurrentes verdes
- integridad de cupo y duplicados bajo concurrencia
- decisión documentada de caché
- decisión documentada de idempotencia
- stress test reproducible
- builds/tests verdes
- PR Ready for review

Enviar por Telegram un resumen final con:

- rama
- HEAD
- PR
- métricas antes/después
- p95/p99
- resultados de concurrencia
- índices agregados o descartados
- caché implementado o descartado y razón
- idempotencia implementada o pendiente y razón
- resultados k6
- riesgos
- siguiente paso recomendado

No iniciar otro bloque funcional al terminar.

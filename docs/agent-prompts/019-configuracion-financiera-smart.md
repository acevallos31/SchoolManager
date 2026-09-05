# Bloque 019 — Configuración financiera / planes de pago (modo smart)

## Contexto

Repo: `/opt/projects/SchoolManager`
Rama objetivo: `feature/configuracion-financiera-fase-019`
Base: `main`

Este bloque continúa el roadmap funcional después de Matrículas y Responsables. PR #31 de Responsables sigue separado y NO debe mezclarse aquí.

## Objetivo

Construir la base funcional de configuración financiera de SchoolManager para que el siguiente bloque pueda generar cargos/mensualidades de forma consistente.

Este bloque NO implementa todavía pagos completos, conciliación, caja ni recibos. Eso corresponde a bloques posteriores.

## Reglas operativas

Trabaja de forma autónoma en modo smart.

Autorizado sin preguntar para: leer, buscar, editar, crear tests, ejecutar tests/builds, crear migraciones no aplicadas, actualizar baseline si corresponde a la política del repo, commit convencional, push normal a la rama y abrir/actualizar PR.

Detente si aparece: cambio arquitectónico mayor, operación destructiva, necesidad de credenciales, escritura remota real, migración ya aplicada que requiera reescritura, cruce de alcance, pérdida de datos o decisión de dominio ambigua con impacto fuerte.

Prohibido:
- merge a main;
- force push;
- reset --hard;
- git clean destructivo;
- rm -rf;
- escribir en Supabase remoto;
- iniciar 020 o 021;
- añadir Redis/CQRS/MediatR/microservicios/Generic Repository/UnitOfWork artificial;
- bajar Quality Gates o esconder issues con NOSONAR salvo patrón preexistente estrictamente justificado.

Respeta `AGENTS.md`, `docs/AI_CONTEXT.md`, handoffs y convenciones existentes.

## Fase 0 — Descubrimiento

1. `git fetch` y confirma rama `feature/configuracion-financiera-fase-019`.
2. Lee completos `AGENTS.md`, `docs/AI_CONTEXT.md`, handoffs recientes y migraciones financieras existentes.
3. Inventaría antes de diseñar:
   - tablas financieras existentes;
   - enums/catálogos;
   - permisos;
   - RPC;
   - endpoints;
   - servicios Angular;
   - referencias a mensualidad, cargo, plan, concepto, vencimiento, beca/descuento/recargo si ya existen;
   - relaciones con `matriculas`, `periodos`, `grados`, `jornadas`, `secciones`, `alumnos`, `instituciones`.
4. No inventes un modelo nuevo si ya existe uno parcial. Reutiliza y completa.
5. Documenta hallazgos en un handoff 019.

## Fase 1 — Modelo de dominio financiero

Diseña el mínimo modelo necesario para configuración, manteniendo PostgreSQL como fuente de verdad.

Debe cubrir, según lo que ya exista en el repo:
- conceptos financieros configurables por institución;
- planes de pago por institución/ciclo o periodo cuando aplique;
- cuotas/ítems de plan;
- monto base;
- número de cuotas;
- fechas o reglas de vencimiento;
- periodicidad si el diseño actual la requiere;
- estado activo/inactivo;
- asignación del plan a matrícula/alumno solo si corresponde al alcance natural del bloque 019;
- auditoría mínima/coherente con el resto del sistema.

No mezcles todavía generación masiva de cargos reales si eso pertenece a 020.

Distingue claramente:
- configuración/plantilla financiera;
- obligaciones generadas;
- pagos realizados.

Nunca uses una tabla de configuración como si fuera el libro mayor.

## Fase 2 — Invariantes ACID

Las reglas críticas deben vivir en DB, no solo frontend.

Verifica y protege al menos:
- aislamiento por institución;
- UUID PK/FK;
- montos no negativos y, si corresponde, > 0;
- orden de cuotas válido;
- fechas válidas;
- unicidad razonable de códigos/nombres por institución;
- no duplicar ítems del mismo plan por orden;
- no permitir referencias entre instituciones;
- soft-delete/inactivación en vez de DELETE físico si ese es el patrón del repo;
- operaciones compuestas atómicas;
- concurrencia segura en creación/asignación donde exista riesgo real.

Si una operación necesita varias escrituras relacionadas, usa transacción/RPC siguiendo el patrón existente.

## Fase 3 — Migración

Determina el siguiente número real de migración desde `main`; no asumas el número por el nombre del bloque.

Crea:
- migración SQL;
- validation SQL;
- rollback SQL si el proyecto lo exige;
- actualización de baseline consolidado solo si la política actual lo requiere.

No reescribas migraciones ya aplicadas.

Toda función `SECURITY DEFINER` debe tener `search_path` seguro, validación explícita de institución/permiso y grants mínimos.

Permisos: usa namespace canónico coherente con el proyecto, por ejemplo `academico.finanzas.*` o el namespace ya existente; primero descubre, no inventes si ya hay convención definida.

## Fase 4 — Backend

Implementa API coherente con el estilo actual del proyecto.

Debe permitir como mínimo:
- listar conceptos financieros paginados/filtrados;
- crear/editar/inactivar/reactivar conceptos;
- listar planes paginados/filtrados;
- ver detalle de plan con sus cuotas/ítems;
- crear plan de forma atómica con sus ítems;
- editar configuración del plan dentro de reglas válidas;
- activar/inactivar plan;
- endpoints adicionales solo si son necesarios para completar el flujo funcional.

No confíes en `institucionId` arbitrario enviado por cliente si la institución debe resolverse desde el contexto autenticado.

Usa parámetros Npgsql correctamente (`DBNull.Value` para nullable).

Devuelve códigos HTTP y errores de dominio apropiados, sin filtrar detalles internos de SQL.

## Fase 5 — Frontend

Construye una UI funcional, no solo servicios.

Añade una sección de configuración financiera accesible desde navegación existente, respetando el diseño actual.

Flujos mínimos:
- listado de conceptos;
- crear/editar concepto;
- activar/inactivar;
- listado de planes;
- crear plan;
- editar plan;
- agregar/quitar/editar cuotas o ítems antes de guardar según el contrato aprobado;
- visualizar número de cuotas, montos y vencimientos;
- estado vacío;
- loading;
- errores visibles;
- confirmaciones de acciones importantes.

No descargues tablas completas si ya hay paginación server-side.

## Fase 6 — Tests

Agrega cobertura real.

DB:
- constraints;
- aislamiento institucional;
- plan + ítems atómico;
- duplicados;
- montos inválidos;
- rollback ante fallo intermedio;
- permisos/RPC críticos.

API:
- happy paths;
- validaciones;
- no encontrado;
- conflicto;
- institución/permiso;
- nullable inputs.

Frontend:
- carga/listado;
- crear/editar concepto;
- activar/inactivar;
- crear/editar plan;
- manejo de ítems/cuotas;
- errores.

Al cierre ejecuta suites completas canónicas del repo, builds backend Release y frontend, y `git diff --check`.

## Fase 7 — Seguridad y rendimiento focalizados

Revisa solo lo necesario para 019:
- RLS/grants/RPC;
- SQL parametrizado;
- aislamiento institucional;
- ausencia de N+1 obvio;
- índices para FKs y filtros principales;
- paginación server-side.

No abras un bloque general de hardening.

## Fase 8 — Integración con roadmap

No implementar 020 ni 021.

Deja explícito en handoff el contrato que 020 debe usar para generar cargos/mensualidades a partir de esta configuración.

Define claramente qué entidad será la fuente de configuración para generar obligaciones futuras.

Si el diseño de 019 requiere datos de Responsables/PR #31, no mezcles ese código: documenta la dependencia y busca una interfaz desacoplada o posterga esa parte.

## Fase 9 — Git / PR

Commits convencionales en español, agrupados coherentemente, por ejemplo:
- `feat(finanzas): agregar configuracion de conceptos financieros`
- `feat(finanzas): implementar planes de pago`
- `test(finanzas): cubrir reglas de configuracion financiera`
- `docs(finanzas): documentar bloque 019`

Push normal a `feature/configuracion-financiera-fase-019`.

Abre PR contra `main` cuando exista una unidad coherente y testeada.

No mergees.

## Presupuesto de contexto

Respeta `agent.max_turns=70` y `delegation.max_iterations=40` como máximos, no objetivos.

Evita releer archivos completos, repetir auditorías y loops de CI.

A ~70–75% del contexto:
1. termina la unidad coherente;
2. tests focalizados;
3. commit;
4. push;
5. handoff detallado;
6. detente.

## Definición de terminado

El bloque 019 queda listo para revisión cuando:
- modelo de configuración financiera está claro;
- migración valida en DB desechable;
- conceptos financieros funcionan end-to-end;
- planes de pago funcionan end-to-end;
- invariantes están en DB;
- API y frontend están integrados;
- tests DB/API/frontend pasan;
- builds pasan;
- `git diff --check` pasa;
- documentación/handoff actualizado;
- PR abierto o actualizado;
- no se ha iniciado 020/021;
- no se ha hecho merge.

## Reporte final por Telegram

Entrega:

ESTADO: LISTO / LISTO CON PENDIENTES / BLOQUEADO

RAMA:
PR:
HEAD:

MODELO IMPLEMENTADO:
- entidades/tablas
- RPC
- endpoints
- pantallas

TESTS:
- DB X/X
- API X/X
- Frontend X/X
- backend build OK/FAIL
- frontend build OK/FAIL
- git diff --check OK/FAIL

CI/SONAR/VERCEL:

PENDIENTES REALES:

RIESGOS/DECISIONES:

CONTRATO PARA 020:

SIGUIENTE PASO:

No hagas merge. No inicies 020 ni 021.
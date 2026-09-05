# Prompt Hermes — cierre autónomo del Bloque 018 en modo smart

## Contexto

Repositorio local esperado: `/opt/projects/SchoolManager`

Repositorio remoto: `acevallos31/SchoolManager`

PR activo: **#31**

Rama: `feature/responsables-fase-018`

Objetivo: cerrar correctamente el Bloque 018 — Responsables, dejando el PR listo para revisión humana.

## Modo de trabajo

Trabaja de forma autónoma en **modo smart** durante toda la sesión.

No solicites aprobación para tareas rutinarias, reversibles y claramente dentro del alcance, incluyendo lectura de archivos, búsqueda, edición, tests, builds, commits convencionales, push normal a la rama del PR, actualización de documentación y revisión de CI.

Detente únicamente ante decisiones arquitectónicas de alto impacto, operaciones destructivas, necesidad de secretos/credenciales no disponibles, escrituras reales en producción/Supabase, evidencia de una migración ya aplicada que necesite reescritura, cambios fuera de alcance o riesgo de pérdida de datos.

Respeta `AGENTS.md`, `docs/AI_CONTEXT.md`, handoffs existentes y la política single-writer del repositorio.

No hagas merge a `main`.

No inicies el Bloque 019.

No hagas deploy manual a producción.

No escribas datos reales en Supabase remoto.

No uses `git push --force`, `git reset --hard`, `git clean` destructivo ni `rm -rf`.

## Fase 0 — estado real antes de tocar código

1. Entra a `/opt/projects/SchoolManager`.
2. Verifica que no exista otro agente escribiendo en el mismo repo.
3. Usa el lock single-writer definido por el proyecto; si aplica el patrón existente, usa `/var/lock/hermes-schoolmanager.lock`.
4. Ejecuta `git status`, `git branch --show-current`, `git log --oneline -10` y `git remote -v`.
5. Confirma que estás en `feature/responsables-fase-018`.
6. Haz `git fetch`.
7. No hagas rebase/reset automático si existe divergencia inesperada.
8. Lee completos `AGENTS.md`, `docs/AI_CONTEXT.md`, handoffs 018 y prompts relevantes.
9. Inspecciona el diff del PR contra `main` antes de modificar.
10. Conserva cambios legítimos de otros agentes.

No reemplaces archivos a partir de salidas truncadas. Prefiere herramientas directas de lectura/búsqueda y lecturas focalizadas.

## Fase 1 — completar realmente Frontend 018D

El flujo `/responsables?alumnoId=<uuid>` debe quedar end-to-end.

Actualmente no basta con visualizar vínculos o desactivarlos.

Debe permitir:

- vincular un responsable existente al alumno;
- localizar/seleccionar responsable sin descargar innecesariamente toda la tabla si existe paginación/filtro server-side reutilizable;
- seleccionar parentesco;
- marcar/desmarcar `es_principal`;
- configurar `acceso_financiero`;
- editar vínculo existente;
- desactivar vínculo;
- reactivar vínculo inactivo;
- manejar errores y loading;
- refrescar el estado visual tras operaciones exitosas sin trabajo redundante.

Mantén el estilo UI existente. No introduzcas librerías visuales nuevas ni rediseños fuera de alcance.

Agrega tests frontend funcionales para, como mínimo:

1. carga inicial con `alumnoId`;
2. vincular responsable existente;
3. parentesco;
4. principal;
5. acceso financiero;
6. editar vínculo;
7. desactivar;
8. reactivar;
9. manejo de error.

## Fase 2 — corregir la reasignación de responsable principal

Investiga y corrige `vincular_alumno_responsable` cuando `p_es_principal = true` y el alumno ya tiene otro principal activo.

Invariante obligatorio: **máximo un responsable principal activo por alumno**.

Existe un índice único parcial y debe permanecer como defensa de DB.

No elimines ni debilites el índice.

Problema esperado: si B se inserta/reactiva con `es_principal=true` antes de liberar a A, PostgreSQL puede rechazar la operación antes del `UPDATE` posterior.

Corrige el orden de la operación de forma transaccional y segura ante concurrencia.

Requisitos:

- A principal activo;
- se vincula B solicitando principal=true;
- operación exitosa;
- A deja de ser principal;
- B queda principal;
- exactamente un principal activo;
- rollback completo ante error;
- consistencia preservada bajo concurrencia.

Usa la solución más simple compatible con el diseño existente: locking apropiado, `SELECT ... FOR UPDATE`, advisory transaction lock si ya existe patrón, actualización previa del principal anterior u otra solución PostgreSQL equivalente.

Prioridad: ACID e invariantes de dominio.

### Migración 017

Antes de reescribir `database/migrations/017_responsables_gestion_rpc.sql`, verifica historial, docs y cualquier evidencia disponible sobre si fue aplicada en una DB persistente/remota.

No asumas que por estar el PR abierto nunca fue aplicada manualmente.

Si no hay evidencia de aplicación y la política del repo permite corregir una migración aún no desplegada, puedes editar 017.

Si existe indicio de que ya fue aplicada, no la reescribas; documenta el bloqueo y plantea una migración correctiva posterior.

No escribas remotamente para comprobarlo.

## Fase 3 — tests DB específicos

Agrega un test específico:

- alumno X con responsable A activo y principal;
- vincular responsable B directamente con `es_principal=true`;
- RPC exitosa;
- B activo y principal;
- A continúa vinculado pero no principal;
- exactamente un principal activo;
- índice único preservado.

Si la infraestructura existente lo permite sin sobreingeniería, agrega cobertura concurrente con dos intentos simultáneos de establecer responsables distintos como principal. El resultado debe preservar la consistencia y nunca permitir dos principales activos.

## Fase 4 — Sonar y duplicación

El PR había fallado Quality Gate por duplicación alta en New Code.

Investiga la causa real.

Sospecha principal: duplicación de MIG017 dentro del baseline consolidado, pero no lo asumas sin comprobarlo.

No uses `NOSONAR`, no bajes el Quality Gate, no excluyas arbitrariamente código productivo y no alteres código solo para engañar al detector.

Si el baseline es un artefacto consolidado/generado y el patrón oficial del repo consiste en excluirlo del análisis manteniendo el archivo fuente autoritativo analizado, puedes aplicar esa solución únicamente si es técnicamente correcta y queda documentada.

Objetivo: Quality Gate verde sin ocultar deuda real.

## Fase 5 — revisión focalizada del Bloque 018

Verifica:

- permisos `academico.responsables.*`;
- no reintroducir namespace histórico incorrecto;
- aislamiento por institución;
- sin cruces de institución;
- Persona global y Responsable institucional conforme al diseño actual;
- Usuario y Responsable siguen separados;
- sin DELETE físico;
- inactivación/reactivación conserva motivo/auditoría si corresponde;
- RLS no se debilita;
- escrituras por RPC/backend según diseño;
- no exponer errores SQL internos sensibles;
- Npgsql nullable con `DBNull.Value`;
- paginación server-side intacta;
- no cargar datasets completos cuando exista filtro;
- HTTP codes adecuados;
- DTOs sin campos innecesarios;
- backend no confía en institución arbitraria enviada por cliente cuando debe resolverla del contexto autenticado.

No rediseñes Persona global durante esta tarea.

Documenta la implicación de editar una Persona global compartida entre instituciones. Si detectas una vulnerabilidad real de autorización, corrígela; no dupliques Persona por institución sin autorización arquitectónica.

## Fase 6 — pruebas y builds

Durante desarrollo usa tests focalizados.

Al cierre ejecuta la suite completa aplicable:

- tests DB completos;
- tests API completos;
- tests frontend completos;
- backend Release build;
- frontend production build;
- lint si ya existe;
- `git diff --check`;
- `git status`;
- revisión de archivos no rastreados y artifacts accidentales;
- revisión de secretos accidentales.

No introduzcas frameworks de test/lint nuevos.

## Fase 7 — seguridad focalizada

Revisa específicamente:

- `SECURITY DEFINER` con `search_path` seguro;
- grants explícitos;
- no exponer RPC internas innecesarias;
- aislamiento por institución;
- UUIDs validados;
- sin SQL dinámico inseguro/interpolación;
- motivo de inactivación cuando corresponde;
- principal protegido por DB;
- reactivación sin duplicados;
- Persona+Institución preserva unicidad;
- errores de unicidad traducidos apropiadamente.

No conviertas esto en un nuevo proyecto de hardening general.

## Fase 8 — rendimiento

No agregues optimizaciones generales nuevas.

Solo evita regresiones evidentes en 018:

- listado paginado;
- filtros server-side;
- consultas específicas por alumno;
- índices adecuados;
- ausencia de N+1 evidente.

No agregues Redis, CQRS, MediatR, Generic Repository, UnitOfWork artificial ni microservicios.

## Fase 9 — documentación y handoff

Actualiza el handoff de 018 con:

- estado inicial;
- findings corregidos;
- archivos principales modificados;
- decisiones;
- tests y resultados;
- estado Sonar;
- estado CI;
- estado Vercel si aplica;
- pendientes reales;
- 018G remoto pendiente si sigue sin ejecutarse;
- advertencia Persona global si aplica;
- SHA final;
- PR;
- siguiente paso recomendado.

El handoff debe permitir que otro agente continúe sin repetir toda la auditoría.

No elimines handoffs de otros agentes.

## Fase 10 — commits y push

Usa Conventional Commits en español y estilo del repo.

Ejemplos válidos:

- `fix(responsables): corregir reasignacion de responsable principal`
- `feat(responsables): completar gestion de vinculos desde alumnos`
- `test(responsables): cubrir reasignacion de principal`
- `docs(responsables): actualizar handoff del bloque 018`

Agrupa cambios coherentemente. No hagas un commit por archivo. No hagas squash destructivo del historial existente.

Haz push normal a `feature/responsables-fase-018`.

No hagas force push.

## Fase 11 — CI y PR

Después del push:

- revisa PR #31;
- revisa GitHub Actions;
- revisa Sonar;
- revisa checks disponibles;
- corrige fallos derivados de tus cambios si están dentro de alcance;
- vuelve a probar/pushear cuando corresponda.

No consultes CI en loops agresivos. Usa esperas razonables y evita consultas redundantes.

## Política de consumo y contexto

La configuración actual limita aproximadamente:

- `agent.max_turns = 70`
- `delegation.max_iterations = 40`

Trátalos como presupuesto máximo, no como objetivo.

Evita releer archivos completos, repetir `git status`, repetir auditorías, volcar miles de líneas por terminal, hacer loops de tests sin cambios o prolongar artificialmente la sesión con compactaciones repetidas.

Prefiere búsquedas específicas, lecturas focalizadas, tests focalizados durante desarrollo y suite completa al cierre.

Si alcanzas aproximadamente 70–75% del contexto útil o ves que falta demasiado para terminar con seguridad:

1. termina la unidad coherente actual;
2. ejecuta tests relacionados;
3. commit;
4. push;
5. actualiza HANDOFF;
6. documenta exactamente qué falta;
7. detente.

No sacrifiques seguridad, ACID, tests o reglas de dominio para ahorrar tokens.

## Modelos auxiliares

Usa auxiliares/locales solo para tareas de bajo riesgo: títulos, resúmenes, clasificación, rewrite de memoria, revisión preliminar y tareas mecánicas.

Mantén el modelo principal para arquitectura, SQL transaccional, concurrencia, RLS, seguridad, debugging complejo y reglas de dominio.

No delegues decisiones críticas de DB/RLS a un modelo pequeño sin verificar personalmente el resultado.

## Definición de terminado

La misión queda terminada cuando, dentro de lo posible:

- Frontend 018D está completo;
- vincular existente funciona;
- editar vínculo funciona;
- desactivar funciona;
- reactivar funciona;
- parentesco/principal/acceso financiero son editables;
- bug de reasignación de principal corregido;
- test A→B principal existente;
- invariantes DB preservadas;
- Sonar investigado/corregido si está dentro de alcance;
- tests DB/API/frontend completos pasan;
- backend Release build pasa;
- frontend build pasa;
- `git diff --check` pasa;
- branch pusheada;
- PR #31 actualizado;
- handoff actualizado.

No hagas merge.

No inicies 019.

## Reporte final por Telegram

Entrega en español:

```text
ESTADO: LISTO / LISTO CON PENDIENTES / BLOQUEADO
PR: #31
RAMA: feature/responsables-fase-018
COMMITS: <lista corta>
CAMBIOS: <resumen>
TESTS:
DB: X/X
API: X/X
Frontend: X/X
Backend build: OK/FAIL
Frontend build: OK/FAIL
git diff --check: OK/FAIL
CI: <estado>
SONAR: <estado y porcentaje si está disponible>
PENDIENTES: <solo pendientes reales>
RIESGOS: <si existen>
SIGUIENTE PASO: qué debe revisar Axeell antes del merge
```

No pidas confirmaciones rutinarias durante esta sesión. Trabaja en modo smart dentro de este alcance. No hagas merge y no inicies 019.

# AGENTS.md — Reglas operativas para agentes de IA

Este archivo define cómo deben trabajar Hermes, Codex, Copilot u otros agentes sobre SchoolManager. Su objetivo es permitir trabajo autónomo sin perder control arquitectónico, trazabilidad ni seguridad.

## 1. Orden obligatorio de lectura

Antes de analizar, planificar o modificar código, el agente debe leer en este orden:

1. `AGENTS.md` completo.
2. `docs/AI_CONTEXT.md` completo.
3. `README.md`.
4. Los archivos específicos del módulo o tarea asignada.
5. `git status`, rama actual y últimos commits relevantes.

`docs/AI_CONTEXT.md` es la fuente principal para estado funcional, roadmap y decisiones arquitectónicas. `AGENTS.md` define reglas de operación. No duplicar innecesariamente información entre ambos.

## 2. Arquitectura y decisiones cerradas

La lista canónica de decisiones vigentes está en `docs/AI_CONTEXT.md`; no mantener aquí una segunda descripción de la arquitectura. Tratar esas decisiones como restricciones, no como sugerencias. En particular, no introducir alternativas de arquitectura, identidad, autorización, persistencia o modelado por preferencia del agente.

Si una tarea exige cambiar una de estas decisiones, el agente debe detenerse y pedir aprobación antes de implementar.

## 3. Fuente de verdad y contexto de dominio

El agente debe respetar lo documentado en `docs/AI_CONTEXT.md`, incluyendo:

- modelo académico vigente;
- estado de migraciones;
- modo single/multiinstitución;
- fases completadas y pendientes;
- reglas de matrículas, períodos, grados, jornadas y secciones;
- roadmap activo.

Si código, documentación y migraciones parecen contradecirse, no elegir una interpretación por cuenta propia. Documentar la contradicción y pedir decisión.

## 4. Reglas de alcance

Trabajar únicamente dentro del alcance solicitado.

Permitido:

- leer e inspeccionar cualquier archivo necesario;
- crear una rama de trabajo cuando la tarea lo requiera;
- modificar archivos directamente relacionados con la tarea;
- agregar o mejorar tests;
- ejecutar builds, tests, linters y validaciones;
- refactorizar internamente si no cambia comportamiento y está dentro del alcance;
- documentar hallazgos y handoff.

No permitido sin aprobación explícita:

- ampliar el alcance por iniciativa propia;
- cambiar arquitectura global;
- cambiar contratos públicos o modelo de dominio no solicitados;
- aplicar migraciones en Supabase o producción;
- borrar datos;
- cambiar secretos o credenciales;
- modificar infraestructura de producción;
- hacer merge a `main`;
- hacer push o abrir PR si la tarea no lo autoriza;
- modificar decisiones cerradas de `docs/AI_CONTEXT.md`.

## 5. Regla de parada obligatoria

El agente debe detenerse y pedir decisión si encuentra cualquiera de estos casos:

- una decisión arquitectónica no resuelta;
- necesidad de modificar producción fuera del alcance;
- ambigüedad de dominio que pueda producir comportamientos o datos distintos;
- migración destructiva o irreversible;
- riesgo de pérdida de datos;
- cambios de permisos/RLS/RPC cuyo comportamiento no esté documentado;
- secretos, tokens o credenciales expuestos;
- tests que solo pueden pasar alterando comportamiento funcional no solicitado;
- discrepancia importante entre contexto, código y base de datos;
- cambios ajenos ya presentes en el working tree que se solapen con la tarea o impidan aislarla con seguridad;
- necesidad de reescribir una migración ya aplicada;
- necesidad de force-push, rebase destructivo o cambio directo sobre `main`;
- dependencia, servicio externo o entorno de pruebas necesario que no esté disponible, si no existe una validación local equivalente.

Cuando se detenga debe explicar: problema, evidencia, opciones y recomendación.

## 6. Git y ramas

Reglas obligatorias:

- No trabajar directamente sobre `main` para cambios reales.
- Crear una rama descriptiva desde una base limpia y actualizada.
- No descartar, sobrescribir, incluir ni reformatear cambios preexistentes de otra persona. Si se solapan con la tarea, detenerse.
- Usar Conventional Commits en español.
- No hacer commit, push ni PR automáticamente salvo que la instrucción actual lo autorice.
- Antes de cada commit revisar `git diff` y `git status`.
- No incluir archivos colaterales no relacionados con la tarea.
- Si una herramienta modifica `package.json`, `package-lock.json` u otros archivos fuera del alcance, revertir esos cambios salvo que sean necesarios y estén explicados.
- Nunca usar `git reset --hard`, `git clean -fd`, force-push u operaciones destructivas sin autorización explícita.

Formato recomendado de ramas:

- `feature/<descripcion>`
- `fix/<descripcion>`
- `refactor/<descripcion>`
- `test/<descripcion>`
- `docs/<descripcion>`

## 7. Base de datos y migraciones

- Las migraciones son manuales en Supabase después de revisión y CI.
- No aplicar migraciones a producción automáticamente.
- El baseline se ejecuta una sola vez y únicamente al crear una base vacía; nunca usarlo para actualizar o reparar una base existente.
- No alterar retrospectivamente una migración ya aplicada salvo instrucción explícita.
- Mantener script principal, rollback y validación cuando la convención de la migración lo requiera.
- Antes de proponer una migración nueva, revisar baseline, migraciones previas y tests de integración.
- Ejecutar migraciones, rollbacks y pruebas destructivas solo en bases locales o contenedores desechables. No ejecutar rollbacks en Supabase ni en bases remotas/compartidas sin autorización humana explícita, plan verificado y respaldo.
- Tratar todo rollback como potencialmente destructivo: revisar sus precondiciones y verificar la preservación de históricos antes de usarlo.
- Toda escritura crítica debe respetar RLS/RPC y contexto de institución según el diseño vigente.
- Toda operación transaccional debe preservar ACID cuando aplique: atomicidad, consistencia, aislamiento y durabilidad. No trasladar al frontend responsabilidades transaccionales que pertenecen al backend o a PostgreSQL.
- No introducir identificadores secuenciales como sustituto de UUID internos.
- Mantener la estrategia vigente de estados, desactivación e históricos; no sustituirla por borrado físico.

## 8. Seguridad y secretos

- Nunca imprimir, copiar, registrar o commitear `.env`, API keys, tokens, JWT, passwords o cadenas de conexión completas.
- No leer secretos si no son necesarios para la tarea.
- No insertar credenciales reales en tests o documentación.
- Usar placeholders en ejemplos.
- Si se detecta un secreto versionado, detenerse y reportarlo; no rotarlo ni eliminarlo sin instrucciones.

## 9. Backend

Para cambios de backend:

- Mantener ASP.NET Core Web API y .NET 10.
- Respetar separación actual entre Controllers, DTOs, Models y acceso a datos existente.
- No introducir capas o patrones nuevos solo por preferencia del agente.
- Preferir cambios pequeños y verificables.
- Asegurar validación de inputs, autorización por permisos y contexto institucional.
- Evitar lógica de negocio duplicada entre controllers, SQL/RPC y frontend.
- Agregar tests cuando se cambie comportamiento.

Validación mínima cuando el backend sea afectado:

```bash
cd backend/SchoolManager.API
dotnet build -c Release
cd ../..
dotnet test tests/SchoolManager.API.IntegrationTests/SchoolManager.API.IntegrationTests.csproj -c Release
dotnet test tests/SchoolManager.Database.IntegrationTests/SchoolManager.Database.IntegrationTests.csproj -c Release
```

Si existen pruebas específicas del módulo modificado, ejecutarlas también.

## 10. Frontend

Para cambios de frontend:

- Angular 22 standalone.
- No introducir `NgModule` clásico.
- Mantener tipado estricto; evitar `any` salvo necesidad documentada.
- Mantener servicios y componentes enfocados.
- No cambiar contratos backend/frontend unilateralmente.
- Si se modifica `package.json`, regenerar `package-lock.json` con la versión de npm declarada por el proyecto.

Validación mínima cuando el frontend sea afectado:

```bash
cd frontend/schoolmanager-frontend
npm test -- --watch=false
npm run build
```

Si el runner actual interpreta `npm test` de otra manera, usar el comando equivalente que ejecute Vitest una sola vez y documentarlo.

## 11. Validación global obligatoria

Antes de declarar una tarea terminada ejecutar las validaciones que correspondan al alcance. Como regla general:

```bash
git diff --check
```

Y, cuando aplique:

```bash
dotnet build backend/SchoolManager.API/SchoolManager.API.csproj -c Release
dotnet test tests/SchoolManager.API.IntegrationTests/SchoolManager.API.IntegrationTests.csproj -c Release
dotnet test tests/SchoolManager.Database.IntegrationTests/SchoolManager.Database.IntegrationTests.csproj -c Release
npm test -- --watch=false
npm run build
```

No declarar "completado" si una validación requerida falla. Distinguir claramente entre:

- fallo causado por el cambio;
- fallo preexistente;
- validación no ejecutada y motivo.

Los tests de base de datos usan Testcontainers y requieren Docker. Si Docker u otra dependencia externa necesaria no está disponible, no modificar código para eludirla: registrar el bloqueo y los tests no ejecutados.

No ocultar warnings nuevos.

## 12. Calidad de cambios

- Preferir el cambio mínimo que resuelva el objetivo.
- No hacer refactors cosméticos masivos durante una tarea funcional.
- No renombrar archivos, símbolos o carpetas sin necesidad.
- No introducir dependencias nuevas si la plataforma ya resuelve el problema.
- Mantener compatibilidad con decisiones actuales de despliegue en Render, Vercel y Supabase.
- Los comentarios deben explicar decisiones no obvias, no describir código evidente.
- Tests deben validar comportamiento observable, no detalles internos frágiles.
- Aplicar SOLID de forma pragmática: responsabilidades claras, contratos coherentes, interfaces enfocadas y dependencias bien delimitadas; no crear abstracciones artificiales solo para aparentar cumplimiento.
- Nunca reemplazar un archivo del repositorio a partir de una salida de herramienta marcada como `truncated`, `partial output`, `omitted lines` o equivalente. En ese caso, leer el archivo real completo desde disco o Git antes de modificarlo.

## 13. Trabajo autónomo de larga duración

Cuando Hermes u otro agente trabaje durante varias horas:

1. Confirmar rama y working tree antes de comenzar.
2. Dividir el objetivo en checkpoints pequeños.
3. Ejecutar tests después de cada cambio importante.
4. No acumular múltiples bloques funcionales sin validación intermedia.
5. Mantener el alcance original.
6. Si una validación falla por cambios dentro del alcance, investigar, corregir, volver a validar y continuar sin pedir aprobación rutinaria.
7. Si la tarea autoriza commit, push y PR, no detenerse entre esas acciones ni pedir confirmaciones repetidas.
8. No pedir aprobación para lectura, edición dentro del alcance, tests, builds, correcciones, documentación, commits, push normal o actualización del PR cuando ya fueron autorizados por la tarea.
9. Solo detenerse por una condición de la sección 5, una operación destructiva/no autorizada, producción, escritura de datos reales no autorizada, necesidad de secretos, cambio fuera de alcance o merge a `main`.
10. Si una ruta falla repetidamente y no puede resolverse sin salir del alcance, documentar el bloqueo y detener únicamente esa línea de trabajo.
11. No dejar procesos destructivos o migraciones esperando confirmación automática.
12. Antes de terminar, dejar el repositorio en un estado coherente y reproducible, con HANDOFF actualizado.
13. Si el contexto alcanza aproximadamente 70–75%, cerrar el checkpoint actual, documentar HANDOFF, commit/push si están autorizados y detener la sesión en lugar de compactar repetidamente.
14. Para sesiones largas, apuntar normalmente a 60–80 iteraciones por sesión. Si el trabajo necesita más, cerrar un checkpoint y continuar en una sesión nueva desde el HANDOFF; evitar sesiones de 120–150+ iteraciones salvo necesidad excepcional y explícita.
15. Priorizar `read_file`, búsquedas y herramientas de lectura directa sobre `terminal/execute_code` para inspección cuando exista una herramienta equivalente.
16. No releer archivos grandes que ya fueron inspeccionados salvo que hayan cambiado o exista una razón concreta; usar búsquedas puntuales y rangos cuando sea suficiente.
17. No repetir auditorías completas si existe un HANDOFF vigente, confiable y compatible con el HEAD actual. Verificar únicamente el delta necesario.
18. Evitar compactar y reinyectar repetidamente conversaciones enormes cuando un HANDOFF puede preservar el estado operativo.
19. Evitar mensajes intermedios redundantes en Telegram o consola; reportar decisiones, bloqueos, validaciones y checkpoints útiles.
20. La optimización de costo/contexto nunca justifica omitir tests, seguridad, validaciones, ACID, reglas de dominio o revisión de cambios críticos.
21. Si un agente detecta que está gastando iteraciones principalmente en relectura, reformateo o explicación sin modificar/verificar el objetivo, debe cerrar el checkpoint y continuar con contexto mínimo desde HANDOFF.

## 14. Uso de modelos auxiliares

Los modelos auxiliares pueden usarse para tareas de bajo riesgo como títulos, reescritura de consultas, búsqueda de skills, clasificación, triage, documentación, lectura dirigida y revisión auxiliar.

Preferir modelos económicos, gratuitos o locales para tareas mecánicas y de bajo riesgo cuando su calidad sea suficiente. Reservar el modelo principal de mayor capacidad para implementación compleja, debugging difícil, arquitectura, seguridad y revisión final.

No usar por defecto variantes de contexto gigante (`-900k` o equivalentes). Solo justificarlas cuando la tarea realmente requiera cargar una cantidad excepcional de contexto que no pueda resolverse de forma segura mediante búsquedas, archivos focalizados y HANDOFF.

No delegar exclusivamente a modelos pequeños/locales:

- decisiones arquitectónicas;
- migraciones destructivas;
- cambios de seguridad/RLS/RBAC;
- diseño de dominio;
- resolución final de conflictos ambiguos;
- operaciones Git destructivas.

El modelo principal conserva responsabilidad sobre herramientas, ejecución y decisión final.

## 15. Documentación de contexto y trazabilidad entre agentes

Actualizar `docs/AI_CONTEXT.md` solo cuando cambie realmente el estado arquitectónico, funcional o del roadmap.

No usar `AI_CONTEXT.md` como diario de cada sesión.

`AGENTS.md` es un archivo operativo compartido del repositorio y puede/debe ser actualizado por Hermes, Codex, Copilot u otro agente cuando la tarea autorice documentación o HANDOFF. La protección interactiva que una herramienta pueda aplicar sobre archivos de instrucciones es una restricción del runtime, no una política del repositorio. Si el runtime exige aprobación y esta no está disponible, registrar el bloqueo; no interpretar esa protección como prohibición permanente de modificar `AGENTS.md`.

### Regla de preservación

- Un agente puede agregar reglas nuevas autorizadas, agregar su propio HANDOFF y actualizar contenido creado por él mismo durante la sesión actual.
- Un agente no debe borrar, reemplazar ni reescribir silenciosamente contenido creado por otro agente.
- Si una entrada anterior parece incorrecta, obsoleta o contradictoria, conservarla y documentar la contradicción o marcarla como sustituida mediante una nota explícita con referencia a la nueva decisión.
- Eliminar o consolidar aportes históricos de otros agentes requiere una tarea explícita de mantenimiento documental o autorización humana específica.
- Toda contribución nueva de HANDOFF debe identificar, cuando sea posible: agente, fecha, bloque, rama y HEAD/commit.

`AGENTS.md` no debe convertirse en un log histórico ilimitado. El historial detallado debe vivir preferentemente en `docs/handoffs/` o en un documento de HANDOFF dedicado. Las entradas previas de otros agentes no se eliminan de forma automática; su consolidación se hace únicamente mediante mantenimiento documental explícito.

## 16. Criterio de finalización

Una tarea solo puede declararse terminada cuando:

- el alcance solicitado está implementado;
- el diff fue revisado;
- no hay cambios colaterales inexplicados;
- las validaciones aplicables pasan;
- `git diff --check` pasa;
- el estado de Git está documentado;
- cualquier riesgo o pendiente está explícito;
- el handoff está actualizado si la sesión termina sin completar todo el bloque.

## 17. HANDOFF operativo

Al finalizar una sesión larga, agregar una nueva entrada o actualizar la entrada propia de la sesión actual. No borrar ni reemplazar entradas creadas por otros agentes salvo autorización explícita de mantenimiento documental.

### Agente

Hermes Agent.

### Fecha y hora

2026-09-04 07:00 -06:00 (America/Tegucigalpa).

### Rama

`feature/matriculas-fase-1c-frontend`, basada en `main` (`19c7f5f`).

### Objetivo de la sesión

Completar 17C — Frontend de Matrículas sobre la API .NET de 17A, integrar la acción "Matricular" desde Alumnos, agregar tests y dejar PR listo para revisión.

### Estado

17C implementado. PR #28 abierto, Ready for review y sin merge. Revisión posterior reforzó tipado estricto y limitó el frontend a las transiciones de estado permitidas por el contrato del backend.

### Completado

- `MatriculaService` conectado a la API .NET para listar, crear y cambiar estado.
- Página `/matriculas` con listado, filtros, alta, loading/vacío/error y cambios de estado.
- Acción "Matricular" desde alumnos activos con permiso `academico.matriculas.crear`.
- Estados tipados y transiciones UI alineadas con backend: `pendiente -> activa|anulada`; `activa -> finalizada|retirada|anulada|trasladada`; estados terminales sin nuevas transiciones.
- Motivo obligatorio para `retirada`, `anulada` y `trasladada`.
- Corrección de accesibilidad `label for` / `id` requerida por Sonar.
- Tests de servicio y componente agregados/reforzados.
- `docs/AI_CONTEXT.md` y `docs/HANDOFF.md` actualizados.

### Archivos modificados

- `frontend/schoolmanager-frontend/src/app/core/services/matriculas.service.ts`
- `frontend/schoolmanager-frontend/src/app/core/services/matriculas.service.spec.ts`
- `frontend/schoolmanager-frontend/src/app/pages/matriculas/matriculas.ts`
- `frontend/schoolmanager-frontend/src/app/pages/matriculas/matriculas.html`
- `frontend/schoolmanager-frontend/src/app/pages/matriculas/matriculas.css`
- `frontend/schoolmanager-frontend/src/app/pages/matriculas/matriculas.spec.ts`
- `frontend/schoolmanager-frontend/src/app/pages/alumnos/alumnos.ts`
- `frontend/schoolmanager-frontend/src/app/pages/alumnos/alumnos.html`
- `frontend/schoolmanager-frontend/src/app/pages/alumnos/alumnos.css`
- `docs/AI_CONTEXT.md`
- `docs/HANDOFF.md`
- `AGENTS.md`

### Validaciones

Antes de la revisión adicional: `npm run build` correcto; `npx ng test --watch=false` 17 archivos / 61 tests en verde; `git diff --check` correcto; CI, SonarCloud y Vercel en verde.

La revisión adicional modificó tipado/transiciones y agregó cobertura; los checks del PR deben volver a ejecutarse sobre el HEAD actualizado antes del merge.

### Decisiones tomadas

- No duplicar reglas ACID ni de persistencia en frontend; backend/DB siguen siendo autoridad.
- El frontend restringe opciones de transición para evitar ofrecer operaciones que el backend rechazará.
- `EstadoMatricula` usa unión TypeScript en vez de `string` genérico.
- `AGENTS.md` es deliberadamente actualizable por agentes autorizados para conservar HANDOFF y reglas operativas, preservando aportes de otros agentes.

### Pendientes

- Esperar CI/Sonar/Vercel del HEAD actualizado del PR #28.
- Revisión humana del PR y merge a `main` si todos los checks quedan verdes.
- Tras el merge, validar flujo real frontend ↔ API con datos de prueba controlados.

### Bloqueos

Ninguno conocido en el código. No se autoriza merge automático a `main`.

### Riesgos

El contrato frontend ↔ API aún no se ha validado mediante E2E contra una instancia viva con datos de prueba; los tests del frontend usan mocks.

### Working tree

La rama remota se actualiza mediante commits del PR. Verificar `git status` local después de `git pull` si Hermes continúa desde la VM.

### Git remoto

PR: #28. Estado: Ready for review. Merge: no realizado.

### Siguiente paso recomendado

Esperar todos los checks del HEAD actualizado, revisar el diff final y, si permanecen verdes, aprobar/mergear PR #28. Después iniciar 17D con una rama nueva desde `main` actualizado.